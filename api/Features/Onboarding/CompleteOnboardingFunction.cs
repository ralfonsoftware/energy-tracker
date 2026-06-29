using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EnergyTracker.Api.Features.Onboarding;

public class CompleteOnboardingFunction(AppDbContext db, OnboardingValidator validator)
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Function("CompleteOnboarding")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/onboarding")] HttpRequest req,
        FunctionContext context,
        CancellationToken ct)
    {
        CompleteOnboardingRequest? body = null;
        try
        {
            body = await JsonSerializer.DeserializeAsync<CompleteOnboardingRequest>(req.Body, _jsonOptions, ct);
        }
        catch (JsonException) { }

        if (body is null)
            return new BadRequestObjectResult(new { type = "https://tools.ietf.org/html/rfc7231#section-6.5.1", title = "Bad Request", status = 400, detail = "Invalid request body." });

        var validationResult = await validator.ValidateAsync(body, ct);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return new BadRequestObjectResult(new { type = "https://tools.ietf.org/html/rfc7231#section-6.5.1", title = "Bad Request", status = 400, detail = errors });
        }

        var userId = context.GetUserId();

        if (await db.Flats.AnyAsync(f => f.UserId == userId, ct))
            return new ConflictObjectResult(new { type = "https://tools.ietf.org/html/rfc9110#section-15.5.10", title = "Conflict", status = 409, detail = "Onboarding already completed." });

        var flat = new Flat
        {
            UserId = userId,
            Name = body.FlatName,
            AnnualKwhBaseline = body.AnnualKwhBaseline,
            SpikeThreshold = 2.0m,
            PlannedAnnualSpend = body.PlannedAnnualSpend,
        };
        db.Flats.Add(flat);

        var tariff = new Tariff
        {
            FlatId = flat.FlatId,
            EffectiveDate = DateTimeOffset.UtcNow,
            PricePerKwh = body.PricePerKwh,
            MonthlyBaseFee = body.MonthlyBaseFee,
            ProviderName = body.ProviderName,
            ContractStartDate = body.ContractStartDate,
            ContractDurationMonths = body.ContractDurationMonths,
        };
        db.Tariffs.Add(tariff);
        await db.SaveChangesAsync(ct);

        return new CreatedResult($"/api/v1/flats/{flat.FlatId}", null);
    }
}
