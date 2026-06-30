using EnergyTracker.Api.Data;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace EnergyTracker.Api.Features.Flats;

public class PatchFlatFunction(AppDbContext db, PatchFlatValidator validator)
{
    [Function("PatchFlat")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/flats/{flatId}")] HttpRequest req,
        string flatId,
        FunctionContext context,
        CancellationToken ct)
    {
        var userId = context.GetUserId();

        if (!Guid.TryParse(flatId, out var flatGuid))
            return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "Invalid flatId format." });

        var flat = await db.Flats.FirstOrDefaultAsync(f => f.FlatId == flatGuid, ct);

        if (flat is null || flat.UserId != userId)
            return new ObjectResult(new { title = "Forbidden", status = 403, detail = "Flat not found or access denied." }) { StatusCode = 403 };

        using var reader = new StreamReader(req.Body);
        var body = await reader.ReadToEndAsync(ct);
        JsonNode? node = null;
        try { node = JsonNode.Parse(body); } catch (System.Text.Json.JsonException) { }

        if (node is not JsonObject obj)
            return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "Request body must be a JSON object." });

        decimal? kwhBaseline = null;
        if (obj["annualKwhBaseline"] is JsonValue kwhVal && kwhVal.TryGetValue<decimal>(out var kwh))
            kwhBaseline = kwh;
        else if (obj.ContainsKey("annualKwhBaseline") && obj["annualKwhBaseline"] is not null)
            return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "annualKwhBaseline must be a number." });

        decimal? plannedSpend = null;
        if (obj["plannedAnnualSpend"] is JsonValue spendVal && spendVal.TryGetValue<decimal>(out var spend))
            plannedSpend = spend;
        else if (obj.ContainsKey("plannedAnnualSpend") && obj["plannedAnnualSpend"] is not null)
            return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "plannedAnnualSpend must be a number or null." });

        var request = new PatchFlatRequest(
            Name: obj["name"]?.GetValue<string>(),
            AnnualKwhBaseline: kwhBaseline,
            PlannedAnnualSpendProvided: obj.ContainsKey("plannedAnnualSpend"),
            PlannedAnnualSpend: plannedSpend
        );

        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage);
            return new BadRequestObjectResult(new { title = "Validation Error", status = 400, detail = string.Join("; ", errors) });
        }

        if (request.Name is not null) flat.Name = request.Name.Trim();
        if (request.AnnualKwhBaseline is not null) flat.AnnualKwhBaseline = request.AnnualKwhBaseline.Value;
        if (request.PlannedAnnualSpendProvided) flat.PlannedAnnualSpend = request.PlannedAnnualSpend;

        await db.SaveChangesAsync(ct);

        return new OkObjectResult(new FlatResponse(flat.FlatId, flat.Name, flat.AnnualKwhBaseline, flat.PlannedAnnualSpend));
    }
}
