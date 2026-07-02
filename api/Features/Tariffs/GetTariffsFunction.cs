using EnergyTracker.Api.Data;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Features.Tariffs;

public class GetTariffsFunction(AppDbContext db)
{
    [Function("GetTariffs")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flats/{flatId}/tariffs")]
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

        var tariffs = await db.Tariffs.AsNoTracking()
            .Where(t => t.FlatId == flatGuid)
            .OrderByDescending(t => t.EffectiveDate)
            .ToListAsync(ct);

        var responses = tariffs.Select(t => new TariffResponse(
            t.TariffId,
            t.EffectiveDate,
            t.PricePerKwh,
            t.MonthlyBaseFee,
            t.ProviderName,
            t.ContractStartDate,
            t.ContractDurationMonths,
            TariffLockPolicy.IsLocked(t.ContractStartDate, t.ContractDurationMonths)))
            .ToList();

        return new OkObjectResult(responses);
    }
}
