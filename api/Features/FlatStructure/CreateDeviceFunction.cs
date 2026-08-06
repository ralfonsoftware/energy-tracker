using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnergyTracker.Api.Features.FlatStructure;

public class CreateDeviceFunction(AppDbContext db, DeviceValidator validator)
{
    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

    [Function("CreateDevice")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/flats/{flatId}/powerpoints/{powerPointId}/devices")]
        HttpRequest req,
        string flatId,
        string powerPointId,
        FunctionContext context,
        CancellationToken ct)
    {
        var userId = context.GetUserId();

        if (!Guid.TryParse(flatId, out var flatGuid))
            return new BadRequestObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Bad Request", status = 400,
                detail = "Invalid flatId format."
            });

        var flat = await db.Flats.SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId, ct);
        if (flat is null)
            return new ObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                title = "Forbidden", status = 403,
                detail = "Flat not found or access denied."
            }) { StatusCode = 403 };

        if (!Guid.TryParse(powerPointId, out var powerPointGuid))
            return new BadRequestObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Bad Request", status = 400,
                detail = "Invalid powerPointId format."
            });

        var powerPoint = await db.PowerPoints
            .SingleOrDefaultAsync(pp => pp.PowerPointId == powerPointGuid && pp.FlatId == flatGuid, ct);
        if (powerPoint is null)
            return new NotFoundObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                title = "Not Found", status = 404,
                detail = "Power point not found."
            });

        DeviceInput? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<DeviceInput>(req.Body, _jsonOptions, ct);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Bad Request", status = 400,
                detail = "Invalid JSON in request body."
            });
        }

        if (request is null)
            return new BadRequestObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Bad Request", status = 400,
                detail = "Request body is required."
            });

        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return new BadRequestObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Validation Error", status = 400,
                detail = errors
            });
        }

        var now = DateTimeOffset.UtcNow;
        var device = new Device
        {
            PowerPointId = powerPoint.PowerPointId,
            Name = request.Name.Trim(),
            Type = request.Type,
            Manufacturer = request.Manufacturer,
            Model = request.Model,
            PurchaseDate = request.PurchaseDate,
            InUseSince = request.InUseSince,
            DecommissionedDate = request.DecommissionedDate,
            ConsumptionApproach = request.ConsumptionApproach,
            EuLabelClass = request.EuLabelClass,
            EuAnnualKwh = request.EuAnnualKwh,
            SelfMeasuredKwh = request.SelfMeasuredKwh,
            SelfMeasuredPeriod = request.SelfMeasuredPeriod
        };
        db.Devices.Add(device);

        db.DeviceAssignmentPeriods.Add(new DeviceAssignmentPeriod
        {
            DeviceId = device.DeviceId,
            PowerPointId = powerPoint.PowerPointId,
            FlatId = flatGuid,
            From = device.InUseSince ?? now,
            To = null
        });

        await db.SaveChangesAsync(ct);

        var response = new DeviceResponse(
            device.DeviceId,
            device.Name,
            device.Type,
            device.Manufacturer,
            device.Model,
            device.PurchaseDate,
            device.InUseSince,
            device.DecommissionedDate,
            device.ConsumptionApproach,
            device.EuLabelClass,
            device.EuAnnualKwh,
            device.SelfMeasuredKwh,
            device.SelfMeasuredPeriod,
            device.RowVersion);

        return new CreatedResult(
            $"/api/v1/flats/{flatId}/powerpoints/{powerPointId}/devices/{device.DeviceId}",
            response);
    }
}
