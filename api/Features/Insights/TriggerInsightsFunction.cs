using System.Text.Json;
using Azure.Storage.Queues;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnergyTracker.Api.Features.Insights;

public class TriggerInsightsFunction(AppDbContext db, QueueServiceClient queueServiceClient, ILogger<TriggerInsightsFunction> logger)
{
    [Function("TriggerInsights")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/flats/{flatId}/insights/trigger")]
        HttpRequest req,
        string flatId,
        FunctionContext context,
        CancellationToken ct)
    {
        var userId = context.GetUserId();

        if (!Guid.TryParse(flatId, out var flatGuid))
            return new BadRequestObjectResult(new
            {
                title = "Bad Request", status = 400,
                detail = "Invalid flatId format."
            });

        var flat = await db.Flats.AsNoTracking()
            .SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId, ct);
        if (flat is null)
            return new ObjectResult(new
            {
                title = "Forbidden", status = 403,
                detail = "Flat not found or access denied."
            }) { StatusCode = 403 };

        var existingRun = await GetActiveRunAsync(flatGuid, ct);
        if (existingRun is not null)
            return new AcceptedResult(location: null, new TriggerInsightsResponse(existingRun.RunId));

        var run = new InsightRun
        {
            FlatId = flatGuid,
            Status = InsightRunStatus.Pending,
            StartedAt = DateTimeOffset.UtcNow
        };
        db.InsightRuns.Add(run);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // IX_InsightRuns_FlatId_ActiveOnly (filtered unique index) rejected the insert
            // because a concurrent request already created an active run for this flat.
            var concurrentRun = await GetActiveRunAsync(flatGuid, ct);
            if (concurrentRun is null)
                throw;
            return new AcceptedResult(location: null, new TriggerInsightsResponse(concurrentRun.RunId));
        }

        var queueClient = queueServiceClient.GetQueueClient(InsightsConstants.QueueName);
        var message = JsonSerializer.Serialize(new InsightDiscoveryMessage(flatGuid, run.RunId), InsightsConstants.MessageJsonOptions);

        try
        {
            await queueClient.SendMessageAsync(message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to enqueue discovery message for InsightRun {RunId}.", run.RunId);
            run.Status = InsightRunStatus.Failed;
            run.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return new ObjectResult(new
            {
                title = "Bad Gateway", status = 502,
                detail = "Failed to schedule insights discovery. Please try again."
            }) { StatusCode = 502 };
        }

        return new AcceptedResult(location: null, new TriggerInsightsResponse(run.RunId));
    }

    private async Task<InsightRun?> GetActiveRunAsync(Guid flatGuid, CancellationToken ct) =>
        await db.InsightRuns.AsNoTracking()
            .Where(r => r.FlatId == flatGuid && (r.Status == InsightRunStatus.Pending || r.Status == InsightRunStatus.Processing))
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(ct);
}
