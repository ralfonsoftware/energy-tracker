using EnergyTracker.Api.Data;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Features.Readings;

public class GetReadingHistoryFunction(AppDbContext db)
{
    [Function("GetReadingHistory")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flats/{flatId}/readings")]
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

        var readings = await db.MeterReadings.AsNoTracking()
            .Where(r => r.FlatId == flatGuid)
            .OrderByDescending(r => r.ReadingDate)
            .Select(r => new ReadingResponse(r.ReadingId, r.KwhValue, r.ReadingDate, r.IsCorrected, r.OriginalKwhValue))
            .ToListAsync(ct);

        return new OkObjectResult(readings);
    }
}
