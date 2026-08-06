using EnergyTracker.Api.Data;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace EnergyTracker.Api.Features.FlatStructure;

public class DeleteDeviceFunction(AppDbContext db)
{
    [Function("DeleteDevice")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/flats/{flatId}/powerpoints/{powerPointId}/devices/{deviceId}")]
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
            .SingleOrDefaultAsync(d => d.DeviceId == deviceGuid && d.PowerPointId == powerPointGuid && d.PowerPoint.FlatId == flatGuid, ct);
        if (device is null)
            return new NotFoundObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                title = "Not Found", status = 404,
                detail = "Device not found."
            });

        using var reader = new StreamReader(req.Body);
        var body = await reader.ReadToEndAsync(ct);
        JsonNode? node = null;
        try { node = JsonNode.Parse(body); } catch (System.Text.Json.JsonException) { }

        if (node is not JsonObject obj)
            return new BadRequestObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Bad Request", status = 400,
                detail = "Request body must be a JSON object."
            });

        var rowVersionStr = obj["rowVersion"] is JsonValue rowVersionVal && rowVersionVal.TryGetValue<string>(out var rvs) ? rvs : null;
        if (!ConcurrencyExtensions.TryParseRowVersion(rowVersionStr, out var rowVersion))
            return new BadRequestObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Bad Request", status = 400,
                detail = "rowVersion is required."
            });

        // Loaded up front so the InMemory test provider's cascade delete can see and remove
        // every period belonging to this Device, mirroring UpdateFlatStructureFunction's approach.
        await db.DeviceAssignmentPeriods.Where(p => p.DeviceId == device.DeviceId).LoadAsync(ct);

        db.ApplyRowVersionCheck(device, rowVersion);
        db.Devices.Remove(device);

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

        return new NoContentResult();
    }
}
