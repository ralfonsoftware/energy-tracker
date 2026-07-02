using EnergyTracker.Api.Data;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EnergyTracker.Api.Features.Readings;

public class PatchReadingFunction(AppDbContext db, PatchReadingValidator validator)
{
    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    [Function("PatchReading")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/flats/{flatId}/readings/{readingId}")]
        HttpRequest req,
        string flatId,
        string readingId,
        FunctionContext context,
        CancellationToken ct)
    {
        var userId = context.GetUserId();

        if (!Guid.TryParse(flatId, out var flatGuid) || !Guid.TryParse(readingId, out var readingGuid))
            return new BadRequestObjectResult(new
            {
                title = "Bad Request", status = 400,
                detail = "Invalid id format."
            });

        var flat = await db.Flats.SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId, ct);
        if (flat is null)
            return new ObjectResult(new
            {
                title = "Forbidden", status = 403,
                detail = "Flat not found or access denied."
            }) { StatusCode = 403 };

        var reading = await db.MeterReadings
            .SingleOrDefaultAsync(r => r.ReadingId == readingGuid && r.FlatId == flatGuid, ct);
        if (reading is null)
            return new NotFoundObjectResult(new
            {
                title = "Not Found", status = 404,
                detail = "Reading not found."
            });

        PatchReadingRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<PatchReadingRequest>(req.Body, _jsonOptions, ct);
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

        if (request.KwhValue == reading.KwhValue)
        {
            var unchangedResponse = new ReadingResponse(
                reading.ReadingId, reading.KwhValue, reading.ReadingDate, reading.IsCorrected, reading.OriginalKwhValue);
            return new OkObjectResult(unchangedResponse);
        }

        if (!reading.IsCorrected)
            reading.OriginalKwhValue = reading.KwhValue;
        reading.KwhValue = request.KwhValue;
        reading.IsCorrected = true;

        await db.SaveChangesAsync(ct);

        var response = new ReadingResponse(
            reading.ReadingId, reading.KwhValue, reading.ReadingDate, reading.IsCorrected, reading.OriginalKwhValue);
        return new OkObjectResult(response);
    }
}
