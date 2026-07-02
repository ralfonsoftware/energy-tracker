using EnergyTracker.Api.Data;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EnergyTracker.Api.Features.Tariffs;

public class PatchTariffFunction(AppDbContext db, PatchTariffValidator validator)
{
    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

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

        PatchTariffRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<PatchTariffRequest>(req.Body, _jsonOptions, ct);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult(new
            {
                title = "Bad Request", status = 400,
                detail = "Invalid JSON in request body."
            });
        }

        if (request is null)
            return new BadRequestObjectResult(new
            {
                title = "Bad Request", status = 400,
                detail = "Request body is required."
            });

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
        var contractTermFieldsRequested = request.ContractStartDate is not null || request.ContractDurationMonths is not null;
        if (priceFieldsRequested && contractTermFieldsRequested)
            return new BadRequestObjectResult(new
            {
                title = "Bad Request", status = 400,
                detail = "Cannot update price fields and contract terms in the same request."
            });

        var isLocked = TariffLockPolicy.IsLocked(tariff.ContractStartDate, tariff.ContractDurationMonths);
        var lockBlocksPriceUpdate = priceFieldsRequested && isLocked && !request.LockOverride;

        if (request.ProviderName is not null)
            tariff.ProviderName = request.ProviderName;
        if (request.ContractStartDate is not null)
            tariff.ContractStartDate = request.ContractStartDate;
        if (request.ContractDurationMonths is not null)
            tariff.ContractDurationMonths = request.ContractDurationMonths;

        if (!lockBlocksPriceUpdate)
        {
            if (request.PricePerKwh is not null)
                tariff.PricePerKwh = request.PricePerKwh.Value;
            if (request.MonthlyBaseFee is not null)
                tariff.MonthlyBaseFee = request.MonthlyBaseFee.Value;
        }

        await db.SaveChangesAsync(ct);

        if (lockBlocksPriceUpdate)
            return new ObjectResult(new
            {
                type = "tariff-locked",
                title = "Unprocessable Entity", status = 422,
                detail = "Price fields cannot be changed on a locked tariff without lockOverride."
            })
            { StatusCode = 422 };

        var response = new TariffResponse(
            tariff.TariffId,
            tariff.EffectiveDate,
            tariff.PricePerKwh,
            tariff.MonthlyBaseFee,
            tariff.ProviderName,
            tariff.ContractStartDate,
            tariff.ContractDurationMonths,
            TariffLockPolicy.IsLocked(tariff.ContractStartDate, tariff.ContractDurationMonths));

        return new OkObjectResult(response);
    }
}
