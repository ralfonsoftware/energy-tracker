using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EnergyTracker.Api.Features.Readings;

public class SubmitReadingFunction(AppDbContext db, ReadingValidator validator)
{
    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    [Function("SubmitReading")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/flats/{flatId}/readings")]
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

        SubmitReadingRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<SubmitReadingRequest>(
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

        var reading = new MeterReading
        {
            ReadingId = Guid.NewGuid(),
            FlatId = flatGuid,
            KwhValue = request.KwhValue,
            ReadingDate = request.ReadingDate!.Value,
            IsCorrected = false,
            OriginalKwhValue = null
        };

        db.MeterReadings.Add(reading);
        await db.SaveChangesAsync(ct);

        var response = new ReadingResponse(
            reading.ReadingId,
            reading.KwhValue,
            reading.ReadingDate,
            reading.IsCorrected,
            reading.OriginalKwhValue,
            reading.RowVersion);

        return new CreatedResult(
            $"/api/v1/flats/{flatId}/readings/{reading.ReadingId}",
            response);
    }
}
