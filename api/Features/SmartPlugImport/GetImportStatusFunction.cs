using EnergyTracker.Api.Data;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Features.SmartPlugImport;

public class GetImportStatusFunction(AppDbContext db)
{
    [Function("GetImportStatus")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flats/{flatId}/imports/{jobId}")]
        HttpRequest req,
        string flatId,
        string jobId,
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

        if (!Guid.TryParse(jobId, out var jobGuid))
            return new BadRequestObjectResult(new
            {
                title = "Bad Request", status = 400,
                detail = "Invalid jobId format."
            });

        var importJob = await db.ImportJobs
            .SingleOrDefaultAsync(j => j.ImportJobId == jobGuid && j.FlatId == flatGuid, ct);
        if (importJob is null)
            return new NotFoundObjectResult(new
            {
                title = "Not Found", status = 404,
                detail = "Import job not found."
            });

        var response = new ImportJobStatusResponse(
            importJob.ImportJobId,
            importJob.Status,
            importJob.CreatedAt,
            importJob.CompletedAt,
            importJob.ErrorCategory,
            importJob.GapNotifications);

        return new OkObjectResult(response);
    }
}
