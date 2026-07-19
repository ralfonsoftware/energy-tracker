using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EnergyTracker.Api.Features.Tariffs;

public class CreateTariffFunction(AppDbContext db, TariffValidator validator)
{
    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    [Function("CreateTariff")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/flats/{flatId}/tariffs")]
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

        var flat = await db.Flats.SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId, ct);
        if (flat is null)
            return new ObjectResult(new
            {
                title = "Forbidden", status = 403,
                detail = "Flat not found or access denied."
            }) { StatusCode = 403 };

        CreateTariffRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateTariffRequest>(
                req.Body, _jsonOptions, ct);
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

        var contractStartDate = request.ContractStartDate!.Value;

        if (await db.Tariffs.AnyAsync(t => t.FlatId == flatGuid && t.ContractStartDate == contractStartDate, ct))
            return new ConflictObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                title = "Conflict", status = 409,
                detail = "A tariff with this contract start date already exists for this flat."
            });

        var tariff = new Tariff
        {
            FlatId = flatGuid,
            ContractStartDate = contractStartDate,
            PricePerKwh = request.PricePerKwh,
            MonthlyBaseFee = request.MonthlyBaseFee,
            ProviderName = request.ProviderName,
            ContractDurationMonths = request.ContractDurationMonths
        };

        db.Tariffs.Add(tariff);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return new ConflictObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                title = "Conflict", status = 409,
                detail = "A tariff with this contract start date already exists for this flat."
            });
        }

        var response = new TariffResponse(
            tariff.TariffId,
            tariff.ContractStartDate,
            tariff.PricePerKwh,
            tariff.MonthlyBaseFee,
            tariff.ProviderName,
            tariff.ContractDurationMonths,
            TariffLockPolicy.IsLocked(tariff.ContractStartDate),
            tariff.RowVersion);

        return new CreatedResult(
            $"/api/v1/flats/{flatId}/tariffs/{tariff.TariffId}",
            response);
    }
}
