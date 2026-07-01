using EnergyTracker.Api.Data;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Features.Dashboard;

public class GetDashboardFunction(AppDbContext db, KpiCalculator calculator)
{
    [Function("GetDashboard")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flats/{flatId}/dashboard")]
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
            .OrderBy(r => r.ReadingDate)
            .ToListAsync(ct);

        var tariffs = await db.Tariffs.AsNoTracking()
            .Where(t => t.FlatId == flatGuid)
            .OrderBy(t => t.EffectiveDate)
            .ToListAsync(ct);

        var summary = calculator.Compute(flat, readings, tariffs, DateTimeOffset.UtcNow);
        return new OkObjectResult(summary);
    }
}
