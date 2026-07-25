using System.Text.Json;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnergyTracker.Api.Features.Insights;

public class ProcessInsightsFunction(
    AppDbContext db,
    StandbyDetector standbyDetector,
    ReplacementDetector replacementDetector,
    BudgetAlertDetector budgetAlertDetector,
    InvoiceDeviationDetector invoiceDeviationDetector,
    ILogger<ProcessInsightsFunction> logger)
{
    [Function("ProcessInsights")]
    public async Task RunAsync(
        [QueueTrigger(InsightsConstants.QueueName, Connection = "AzureWebJobsStorage")] string message,
        FunctionContext context,
        CancellationToken ct)
    {
        InsightDiscoveryMessage? discoveryMessage;
        try
        {
            discoveryMessage = JsonSerializer.Deserialize<InsightDiscoveryMessage>(message, InsightsConstants.MessageJsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "ProcessInsights received a malformed queue message.");
            // Rethrow so the Functions host's built-in retry/poison-queue policy handles a
            // genuine producer bug, instead of silently discarding a message we can't process.
            throw;
        }

        if (discoveryMessage is null)
        {
            logger.LogError("ProcessInsights received an empty/null queue message.");
            throw new InvalidOperationException("ProcessInsights received a null discovery message after successful deserialization.");
        }

        var run = await db.InsightRuns.SingleOrDefaultAsync(r => r.RunId == discoveryMessage.RunId, ct);
        if (run is null)
        {
            // Expected in practice — e.g. the Flat (and its cascade-deleted InsightRuns) was
            // removed after this message was enqueued but before it was processed.
            logger.LogWarning("InsightRun {RunId} not found for queue message.", discoveryMessage.RunId);
            return;
        }

        try
        {
            run.Status = InsightRunStatus.Processing;
            await db.SaveChangesAsync(ct);

            // Each detector runs inside its own guarded call so one detector's failure
            // never stops the other three (per AC #4) — only a failure outside all four
            // (e.g. persisting a status transition) fails the whole run.
            await RunDetectorSafelyAsync(nameof(StandbyDetector),
                c => standbyDetector.DetectAsync(discoveryMessage.FlatId, discoveryMessage.RunId, c), discoveryMessage.RunId, ct);
            await RunDetectorSafelyAsync(nameof(ReplacementDetector),
                c => replacementDetector.DetectAsync(discoveryMessage.FlatId, discoveryMessage.RunId, c), discoveryMessage.RunId, ct);
            await RunDetectorSafelyAsync(nameof(BudgetAlertDetector),
                c => budgetAlertDetector.DetectAsync(discoveryMessage.FlatId, discoveryMessage.RunId, c), discoveryMessage.RunId, ct);
            await RunDetectorSafelyAsync(nameof(InvoiceDeviationDetector),
                c => invoiceDeviationDetector.DetectAsync(discoveryMessage.FlatId, discoveryMessage.RunId, c), discoveryMessage.RunId, ct);

            run.Status = InsightRunStatus.Complete;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "InsightRun {RunId} failed: unhandled exception.", discoveryMessage.RunId);
            run.Status = InsightRunStatus.Failed;
        }

        try
        {
            run.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist final status for InsightRun {RunId}.", discoveryMessage.RunId);
        }
    }

    private async Task RunDetectorSafelyAsync(string detectorName, Func<CancellationToken, Task> detect, Guid runId, CancellationToken ct)
    {
        try
        {
            await detect(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{DetectorName} failed for InsightRun {RunId}.", detectorName, runId);
        }
    }
}
