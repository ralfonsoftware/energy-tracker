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
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Bad Request", status = 400,
                detail = "Invalid flatId format."
            });

        var flat = await db.Flats.AsNoTracking()
            .SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId, ct);
        if (flat is null)
            return new ObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                title = "Forbidden", status = 403,
                detail = "Flat not found or access denied."
            }) { StatusCode = 403 };

        var skipRaw = req.Query["skip"].ToString();
        var skip = 0;
        if (!string.IsNullOrEmpty(skipRaw))
        {
            if (!int.TryParse(skipRaw, out skip) || skip < 0)
                return new BadRequestObjectResult(new
                {
                    type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    title = "Bad Request", status = 400,
                    detail = "skip must be a non-negative integer."
                });
        }

        var takeRaw = req.Query["take"].ToString();
        var take = 20;
        if (!string.IsNullOrEmpty(takeRaw))
        {
            if (!int.TryParse(takeRaw, out take) || take < 0)
                return new BadRequestObjectResult(new
                {
                    type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    title = "Bad Request", status = 400,
                    detail = "take must be a non-negative integer."
                });
        }
        if (take > 100)
            return new BadRequestObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Bad Request", status = 400,
                detail = "take must not exceed 100."
            });

        var totalCount = await db.MeterReadings.AsNoTracking()
            .CountAsync(r => r.FlatId == flatGuid, ct);

        var readings = await db.MeterReadings.AsNoTracking()
            .Where(r => r.FlatId == flatGuid)
            .OrderByDescending(r => r.ReadingDate)
            .Skip(skip)
            .Take(take)
            .Select(r => new ReadingResponse(r.ReadingId, r.KwhValue, r.ReadingDate, r.IsCorrected, r.OriginalKwhValue, r.RowVersion))
            .ToListAsync(ct);

        return new OkObjectResult(new ReadingHistoryResponse(readings, totalCount));
    }
}
