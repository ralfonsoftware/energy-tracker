using EnergyTracker.Api.Data;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace EnergyTracker.Api.Features.Tariffs;

public class PatchTariffFunction(AppDbContext db, PatchTariffValidator validator)
{
    [Function("PatchTariff")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/flats/{flatId}/tariffs/{tariffId}")]
        HttpRequest req,
        string flatId,
        string tariffId,
        FunctionContext context,
        CancellationToken ct)
    {
        var userId = context.GetUserId();

        if (!Guid.TryParse(flatId, out var flatGuid) || !Guid.TryParse(tariffId, out var tariffGuid))
            return new BadRequestObjectResult(new
            {
                title = "Bad Request", status = 400,
                detail = "Invalid flatId or tariffId format."
            });

        var flat = await db.Flats.SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId, ct);
        if (flat is null)
            return new ObjectResult(new
            {
                title = "Forbidden", status = 403,
                detail = "Flat not found or access denied."
            }) { StatusCode = 403 };

        var tariff = await db.Tariffs
            .SingleOrDefaultAsync(t => t.TariffId == tariffGuid && t.FlatId == flatGuid, ct);
        if (tariff is null)
            return new NotFoundObjectResult(new
            {
                title = "Not Found", status = 404,
                detail = "Tariff not found."
            });

        using var reader = new StreamReader(req.Body);
        var body = await reader.ReadToEndAsync(ct);
        JsonNode? node = null;
        try { node = JsonNode.Parse(body, new JsonNodeOptions { PropertyNameCaseInsensitive = true }); }
        catch (System.Text.Json.JsonException) { }

        if (node is not JsonObject obj)
            return new BadRequestObjectResult(new
            {
                title = "Bad Request", status = 400,
                detail = "Request body must be a JSON object."
            });

        decimal? pricePerKwh = null;
        if (obj["pricePerKwh"] is JsonValue priceVal && priceVal.TryGetValue<decimal>(out var price))
            pricePerKwh = price;
        else if (obj.ContainsKey("pricePerKwh") && obj["pricePerKwh"] is not null)
            return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "pricePerKwh must be a number." });

        decimal? monthlyBaseFee = null;
        if (obj["monthlyBaseFee"] is JsonValue feeVal && feeVal.TryGetValue<decimal>(out var fee))
            monthlyBaseFee = fee;
        else if (obj.ContainsKey("monthlyBaseFee") && obj["monthlyBaseFee"] is not null)
            return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "monthlyBaseFee must be a number." });

        string? providerName = null;
        if (obj["providerName"] is JsonValue providerVal && providerVal.TryGetValue<string>(out var provider))
            providerName = provider;
        else if (obj.ContainsKey("providerName") && obj["providerName"] is not null)
            return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "providerName must be a string or null." });

        int? contractDurationMonths = null;
        if (obj["contractDurationMonths"] is JsonValue durVal && durVal.TryGetValue<int>(out var duration))
            contractDurationMonths = duration;
        else if (obj.ContainsKey("contractDurationMonths") && obj["contractDurationMonths"] is not null)
            return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "contractDurationMonths must be an integer or null." });

        var lockOverride = false;
        if (obj["lockOverride"] is JsonValue lockVal && lockVal.TryGetValue<bool>(out var lockOverrideValue))
            lockOverride = lockOverrideValue;
        else if (obj.ContainsKey("lockOverride") && obj["lockOverride"] is not null)
            return new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "lockOverride must be a boolean." });

        var request = new PatchTariffRequest(
            PricePerKwh: pricePerKwh,
            MonthlyBaseFee: monthlyBaseFee,
            ProviderNameProvided: obj.ContainsKey("providerName"),
            ProviderName: providerName,
            ContractDurationMonthsProvided: obj.ContainsKey("contractDurationMonths"),
            ContractDurationMonths: contractDurationMonths,
            LockOverride: lockOverride);

        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return new BadRequestObjectResult(new
            {
                title = "Validation Error", status = 400,
                detail = errors
            });
        }

        var priceFieldsRequested = request.PricePerKwh is not null || request.MonthlyBaseFee is not null;

        var isLocked = TariffLockPolicy.IsLocked(tariff.ContractStartDate);
        var lockBlocksPriceUpdate = priceFieldsRequested && isLocked && !request.LockOverride;

        if (lockBlocksPriceUpdate)
            return new ObjectResult(new
            {
                type = "tariff-locked",
                title = "Unprocessable Entity", status = 422,
                detail = "Price fields cannot be changed on a locked tariff without lockOverride."
            })
            { StatusCode = 422 };

        if (request.ProviderNameProvided)
            tariff.ProviderName = request.ProviderName;
        if (request.ContractDurationMonthsProvided)
            tariff.ContractDurationMonths = request.ContractDurationMonths;
        if (request.PricePerKwh is not null)
            tariff.PricePerKwh = request.PricePerKwh.Value;
        if (request.MonthlyBaseFee is not null)
            tariff.MonthlyBaseFee = request.MonthlyBaseFee.Value;

        await db.SaveChangesAsync(ct);

        var response = new TariffResponse(
            tariff.TariffId,
            tariff.ContractStartDate,
            tariff.PricePerKwh,
            tariff.MonthlyBaseFee,
            tariff.ProviderName,
            tariff.ContractDurationMonths,
            TariffLockPolicy.IsLocked(tariff.ContractStartDate));

        return new OkObjectResult(response);
    }
}
