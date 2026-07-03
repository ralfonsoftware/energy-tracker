using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using System.Text.Json;

namespace EnergyTracker.Api.Features.Flats;

public class CreateFlatFunction(AppDbContext db, CreateFlatValidator validator)
{
    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    [Function("CreateFlat")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/flats")] HttpRequest req,
        FunctionContext context,
        CancellationToken ct)
    {
        var userId = context.GetUserId();

        CreateFlatRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateFlatRequest>(req.Body, _jsonOptions, ct);
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

        var flat = new Flat
        {
            UserId = userId,
            Name = request.Name!.Trim(),
            AnnualKwhBaseline = request.AnnualKwhBaseline,
            SpikeThreshold = 2.0m,
            PlannedAnnualSpend = request.PlannedAnnualSpend
        };

        db.Flats.Add(flat);
        await db.SaveChangesAsync(ct);

        return new CreatedResult(
            $"/api/v1/flats/{flat.FlatId}",
            new FlatSummary(flat.FlatId, flat.Name, flat.AnnualKwhBaseline, flat.SpikeThreshold, flat.PlannedAnnualSpend));
    }
}
