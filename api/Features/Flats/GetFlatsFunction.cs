using EnergyTracker.Api.Data;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Features.Flats;

public class GetFlatsFunction(AppDbContext db)
{
    [Function("GetFlats")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flats")] HttpRequest req,
        FunctionContext context,
        CancellationToken ct)
    {
        var userId = context.GetUserId();

        var flats = await db.Flats.AsNoTracking()
            .Where(f => f.UserId == userId)
            .Select(f => new FlatSummary(f.FlatId, f.Name, f.AnnualKwhBaseline, f.SpikeThreshold, f.PlannedAnnualSpend))
            .ToListAsync(ct);

        return new OkObjectResult(flats);
    }
}
