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
            if (run.Status != InsightRunStatus.Pending)
            {
                // A concurrent invocation (Azure's visibility-timeout redelivery racing a
                // still-running or already-finished attempt) has already claimed or finished
                // this RunId. Re-assigning Status to Processing when it is already Processing
                // is a no-op value change that EF Core's change tracker would not detect — the
                // SaveChangesAsync below would silently do nothing instead of throwing — and
                // assigning it when already Complete/Failed would be a genuine but wrong value
                // change that succeeds and reopens a finished run. Checking the freshly-loaded
                // Status here, before attempting any transition, is what makes the claim
                // actually exclusive; the RowVersion-based DbUpdateConcurrencyException catch
                // below only covers two invocations racing from the same Pending starting point.
                logger.LogInformation("InsightRun {RunId} redelivery found the run already {Status}; skipping.", discoveryMessage.RunId, run.Status);
                return;
            }

            try
            {
                run.Status = InsightRunStatus.Processing;
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Both invocations loaded the row while it was still Pending (the guard above
                // doesn't catch this case) and raced to commit the Pending-to-Processing
                // transition — a concurrent invocation won and changed the RowVersion first.
                // This is a normal, expected outcome of at-least-once queue delivery, not an
                // error — the winner is already processing, so this invocation must return
                // without touching any Insight rows. Caught here, specifically, so it never
                // falls through to the generic Exception handler below and never enters the
                // stale-cleanup/detector block at all.
                logger.LogInformation("InsightRun {RunId} redelivery lost the processing claim to a concurrent invocation.", discoveryMessage.RunId);
                return;
            }

            // Guards against Azure's at-least-once queue delivery re-invoking RunAsync after a
            // prior attempt was killed mid-run: clear any partial detector writes from that
            // attempt before running the detectors again, so redelivery can never produce
            // duplicate Insight rows. Runs only after this invocation has won the exclusive
            // Processing claim above (via the Status guard and the RowVersion race both), and
            // inside this try block so a failure here also lands the run in Failed status, same
            // as any other failure in this method.
            var staleInsights = await db.Insights.Where(i => i.RunId == discoveryMessage.RunId).ToListAsync(ct);
            if (staleInsights.Count > 0)
            {
                db.Insights.RemoveRange(staleInsights);
                await db.SaveChangesAsync(ct);
            }

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
