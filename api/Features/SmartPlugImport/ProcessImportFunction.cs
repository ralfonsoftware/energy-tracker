using System.Text.Json;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnergyTracker.Api.Features.SmartPlugImport;

public class ProcessImportFunction(
    AppDbContext db,
    EveHomeParser eveHomeParser,
    MerossParser merossParser,
    InterpolationEngine interpolationEngine,
    ReconciliationEngine reconciliationEngine,
    ILogger<ProcessImportFunction> logger)
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Function("ProcessImport")]
    public async Task RunAsync(
        [BlobTrigger("smart-plug-imports/{userId}/{flatId}/{importJobId}.{ext}",
            Source = BlobTriggerSource.EventGrid,
            Connection = "AzureWebJobsStorage")]
        Stream blobStream,
        string userId, string flatId, string importJobId, string ext,
        FunctionContext context,
        CancellationToken ct)
    {
        if (!Guid.TryParse(importJobId, out var jobGuid))
        {
            logger.LogWarning("Blob trigger fired with malformed importJobId {ImportJobId}.", importJobId);
            return;
        }

        var importJob = await db.ImportJobs.SingleOrDefaultAsync(j => j.ImportJobId == jobGuid, ct);
        if (importJob is null)
        {
            logger.LogWarning("ImportJob {ImportJobId} not found for blob trigger.", importJobId);
            return;
        }

        if (importJob.Status is ImportStatus.Complete or ImportStatus.Failed)
        {
            logger.LogWarning(
                "ImportJob {ImportJobId} already in terminal status {Status}; ignoring redelivered trigger.",
                importJobId, importJob.Status);
            return;
        }

        importJob.Status = ImportStatus.Processing;
        if (!await TrySaveAsync(db, importJob, importJobId, logger, ct))
            return;

        try
        {
            await DispatchToParserAsync(importJob, ext, blobStream, ct);
            importJob.Status = ImportStatus.Complete;
        }
        catch (UnreadableFileException ex)
        {
            logger.LogError(ex, "Import job {ImportJobId} failed: file unreadable.", importJobId);
            importJob.Status = ImportStatus.Failed;
            importJob.ErrorCategory = ImportErrorCategory.DataUnreadable;
        }
        catch (ImportServiceUnavailableException ex)
        {
            logger.LogError(ex, "Import job {ImportJobId} failed: service unavailable.", importJobId);
            importJob.Status = ImportStatus.Failed;
            importJob.ErrorCategory = ImportErrorCategory.ServiceUnavailable;
        }
        catch (OverAttributionException ex)
        {
            logger.LogError(ex,
                "Import job {ImportJobId} failed: attributed consumption {AttributedKwh} kWh exceeds main meter total {MainMeterTotal} kWh beyond tolerance {Tolerance} kWh.",
                importJobId, ex.AttributedKwh, ex.MainMeterTotal, ex.Tolerance);
            importJob.Status = ImportStatus.Failed;
            importJob.ErrorCategory = ImportErrorCategory.ProcessingFailed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Import job {ImportJobId} failed: unhandled exception.", importJobId);
            importJob.Status = ImportStatus.Failed;
            importJob.ErrorCategory = ImportErrorCategory.ProcessingFailed;
        }

        importJob.CompletedAt = DateTimeOffset.UtcNow;
        await TrySaveAsync(db, importJob, importJobId, logger, ct);
    }

    private async Task DispatchToParserAsync(ImportJob importJob, string ext, Stream blobStream, CancellationToken ct)
    {
        switch (ext.ToLowerInvariant())
        {
            case "xlsx":
                await eveHomeParser.ParseAndStoreAsync(importJob.FlatId, importJob.PlugId, blobStream, ct);
                break;
            case "csv":
                await merossParser.ParseAndStoreAsync(importJob.FlatId, importJob.PlugId, importJob.OriginalFileName, blobStream, ct);
                break;
            default:
                throw new UnreadableFileException($"Unsupported file extension: {ext}");
        }

        var interpolation = await interpolationEngine.InterpolateGapsAsync(importJob.FlatId, importJob.PlugId, ct);

        if (interpolation.Gaps.Count > 0)
        {
            var notifications = interpolation.Gaps.Select(g => new GapNotification(importJob.PlugId, g.Start, g.End));
            importJob.GapNotifications = JsonSerializer.Serialize(notifications, _jsonOptions);
        }

        if (interpolation.PeriodStart is { } periodStart && interpolation.PeriodEnd is { } periodEnd)
            await reconciliationEngine.ReconcileAsync(importJob.FlatId, periodStart, periodEnd, ct);
    }

    private static async Task<bool> TrySaveAsync(
        AppDbContext db, ImportJob importJob, string importJobId, ILogger logger, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await db.Entry(importJob).ReloadAsync(ct);
            importJob.Status = ImportStatus.Failed;
            importJob.ErrorCategory = ImportErrorCategory.ProcessingFailed;
            importJob.CompletedAt = DateTimeOffset.UtcNow;
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                logger.LogError(ex,
                    "Import job {ImportJobId} hit a second concurrency conflict while recording failure.",
                    importJobId);
            }
            return false;
        }
    }
}
