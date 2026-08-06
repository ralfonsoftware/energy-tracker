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

public class UpdateDeviceFunction(AppDbContext db, DeviceValidator validator)
{
    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

    [Function("UpdateDevice")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/flats/{flatId}/powerpoints/{powerPointId}/devices/{deviceId}")]
        HttpRequest req,
        string flatId,
        string powerPointId,
        string deviceId,
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

        if (!Guid.TryParse(powerPointId, out var powerPointGuid) || !Guid.TryParse(deviceId, out var deviceGuid))
            return new BadRequestObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Bad Request", status = 400,
                detail = "Invalid powerPointId or deviceId format."
            });

        var device = await db.Devices
            .SingleOrDefaultAsync(d => d.DeviceId == deviceGuid && d.PowerPoint.FlatId == flatGuid, ct);
        if (device is null)
            return new NotFoundObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                title = "Not Found", status = 404,
                detail = "Device not found."
            });

        var targetPowerPoint = await db.PowerPoints
            .SingleOrDefaultAsync(pp => pp.PowerPointId == powerPointGuid && pp.FlatId == flatGuid, ct);
        if (targetPowerPoint is null)
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

        if (!ConcurrencyExtensions.TryParseRowVersion(request.RowVersion, out var rowVersion))
            return new BadRequestObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Bad Request", status = 400,
                detail = "rowVersion is required."
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

        device.Name = request.Name.Trim();
        device.Type = request.Type;
        device.Manufacturer = request.Manufacturer;
        device.Model = request.Model;
        device.PurchaseDate = request.PurchaseDate;
        device.InUseSince = request.InUseSince;
        device.DecommissionedDate = request.DecommissionedDate;
        device.ConsumptionApproach = request.ConsumptionApproach;
        device.EuLabelClass = request.EuLabelClass;
        device.EuAnnualKwh = request.EuAnnualKwh;
        device.SelfMeasuredKwh = request.SelfMeasuredKwh;
        device.SelfMeasuredPeriod = request.SelfMeasuredPeriod;

        if (targetPowerPoint.PowerPointId != device.PowerPointId)
        {
            var openPeriod = await db.DeviceAssignmentPeriods
                .SingleOrDefaultAsync(p => p.DeviceId == device.DeviceId && p.To == null, ct);
            if (openPeriod is not null)
                openPeriod.To = now;

            db.DeviceAssignmentPeriods.Add(new DeviceAssignmentPeriod
            {
                DeviceId = device.DeviceId,
                PowerPointId = targetPowerPoint.PowerPointId,
                FlatId = flatGuid,
                From = now,
                To = null
            });

            device.PowerPointId = targetPowerPoint.PowerPointId;
        }

        db.ApplyRowVersionCheck(device, rowVersion);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                title = "Conflict", status = 409,
                detail = "This record was modified by another request. Reload and try again."
            }) { StatusCode = 409 };
        }

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

        return new OkObjectResult(response);
    }
}
