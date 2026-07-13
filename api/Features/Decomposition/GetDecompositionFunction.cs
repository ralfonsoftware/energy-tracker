using System.Globalization;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Features.Decomposition;

public class GetDecompositionFunction(AppDbContext db, DecompositionEngine engine)
{
    [Function("GetDecomposition")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flats/{flatId}/decomposition")]
        HttpRequest req,
        string flatId,
        FunctionContext context,
        CancellationToken ct)
    {
        var userId = context.GetUserId();

        if (!Guid.TryParse(flatId, out var flatGuid))
            return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "Invalid flatId format." });

        var flat = await db.Flats.AsNoTracking()
            .SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId, ct);
        if (flat is null)
            return new ObjectResult(new { title = "Forbidden", status = 403, detail = "Flat not found or access denied." }) { StatusCode = 403 };

        if (!DateOnly.TryParseExact(req.Query["startDate"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate))
            return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "Invalid or missing startDate." });

        if (!DateOnly.TryParseExact(req.Query["endDate"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate))
            return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "Invalid or missing endDate." });

        if (endDate < startDate)
            return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "endDate must not precede startDate." });

        var response = await engine.ComputeAsync(flatGuid, startDate, endDate, ct);
        return new OkObjectResult(response);
    }
}
