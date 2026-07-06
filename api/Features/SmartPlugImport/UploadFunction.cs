using Azure.Storage.Blobs;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnergyTracker.Api.Features.SmartPlugImport;

public class UploadFunction(AppDbContext db, BlobServiceClient blobServiceClient, ILogger<UploadFunction> logger)
{
    private static readonly string[] AllowedExtensions = [".xlsx", ".csv"];

    [Function("UploadImport")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/flats/{flatId}/imports")]
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

        var flat = await db.Flats.SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId, ct);
        if (flat is null)
            return new ObjectResult(new
            {
                title = "Forbidden", status = 403,
                detail = "Flat not found or access denied."
            }) { StatusCode = 403 };

        if (req.Form.Files.Count == 0)
            return new BadRequestObjectResult(new
            {
                title = "Bad Request", status = 400,
                detail = "No file was uploaded."
            });

        var plugIdValues = req.Form["plugId"];
        var plugId = plugIdValues.Count == 1 ? plugIdValues[0]?.Trim() : null;
        if (string.IsNullOrWhiteSpace(plugId))
            return new BadRequestObjectResult(new
            {
                title = "Bad Request", status = 400,
                detail = "A plugId is required."
            });

        var file = req.Form.Files[0];
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return new BadRequestObjectResult(new
            {
                title = "Bad Request", status = 400,
                detail = "File must be a .xlsx or .csv file."
            });

        if (file.Length == 0)
            return new BadRequestObjectResult(new
            {
                title = "Bad Request", status = 400,
                detail = "Uploaded file is empty."
            });

        var importJob = new ImportJob
        {
            FlatId = flatGuid,
            PlugId = plugId,
            Status = ImportStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ImportJobs.Add(importJob);
        await db.SaveChangesAsync(ct);

        var blobClient = blobServiceClient
            .GetBlobContainerClient("smart-plug-imports")
            .GetBlobClient($"{userId}/{flatGuid}/{importJob.ImportJobId}{ext}");

        await using var stream = file.OpenReadStream();
        try
        {
            await blobClient.UploadAsync(stream, overwrite: false, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ImportJob {ImportJobId} failed: unable to write blob to storage.", importJob.ImportJobId);
            importJob.Status = ImportStatus.Failed;
            importJob.ErrorCategory = ImportErrorCategory.ServiceUnavailable;
            importJob.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return new ObjectResult(new
            {
                title = "Service Unavailable", status = 503,
                detail = "Unable to store the uploaded file. Please try again later."
            }) { StatusCode = 503 };
        }

        return new AcceptedResult(location: null, new UploadImportResponse(importJob.ImportJobId));
    }
}
