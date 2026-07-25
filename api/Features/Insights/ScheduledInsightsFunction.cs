using System.Text.Json;
using Azure.Storage.Queues;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnergyTracker.Api.Features.Insights;

public class ScheduledInsightsFunction(AppDbContext db, QueueServiceClient queueServiceClient, ILogger<ScheduledInsightsFunction> logger)
{
    [Function("ScheduledInsights")]
    public async Task RunAsync(
        [TimerTrigger("0 0 2 * * *")] TimerInfo timer,
        FunctionContext context,
        CancellationToken ct)
    {
        // No IsActive flag exists on User/Flat — every Flat row belongs to a user who
        // completed onboarding, so "active users" means all Flat rows, full stop.
        var flatIds = await db.Flats.Select(f => f.FlatId).ToListAsync(ct);
        var queueClient = queueServiceClient.GetQueueClient(InsightsConstants.QueueName);

        var enqueuedCount = 0;
        foreach (var flatId in flatIds)
        {
            try
            {
                var run = new InsightRun
                {
                    FlatId = flatId,
                    Status = InsightRunStatus.Pending,
                    StartedAt = DateTimeOffset.UtcNow
                };
                db.InsightRuns.Add(run);
                await db.SaveChangesAsync(ct);

                var message = JsonSerializer.Serialize(new InsightDiscoveryMessage(flatId, run.RunId), InsightsConstants.MessageJsonOptions);
                try
                {
                    await queueClient.SendMessageAsync(message, ct);
                    enqueuedCount++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Failed to enqueue discovery message for InsightRun {RunId} (Flat {FlatId}).", run.RunId, flatId);
                    run.Status = InsightRunStatus.Failed;
                    run.CompletedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Isolate this flat's failure so one bad flat doesn't abort the nightly
                // run for every flat that hasn't been processed yet.
                logger.LogError(ex, "ScheduledInsights failed to create/enqueue a run for Flat {FlatId}; continuing with remaining flats.", flatId);
            }
        }

        logger.LogInformation("ScheduledInsights enqueued {Count} discovery messages.", enqueuedCount);
    }
}
