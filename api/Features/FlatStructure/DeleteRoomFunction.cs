using EnergyTracker.Api.Data;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace EnergyTracker.Api.Features.FlatStructure;

public class DeleteRoomFunction(AppDbContext db)
{
    [Function("DeleteRoom")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/flats/{flatId}/rooms/{roomId}")]
        HttpRequest req,
        string flatId,
        string roomId,
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

        if (!Guid.TryParse(roomId, out var roomGuid))
            return new BadRequestObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Bad Request", status = 400,
                detail = "Invalid roomId format."
            });

        var room = await db.Rooms.SingleOrDefaultAsync(r => r.RoomId == roomGuid && r.FlatId == flatGuid, ct);
        if (room is null)
            return new NotFoundObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                title = "Not Found", status = 404,
                detail = "Room not found."
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

        // Loaded up front so the InMemory test provider's cascade delete can see and remove every
        // PowerPoint/Device/DeviceAssignmentPeriod belonging to this Room, mirroring DeleteDeviceFunction's approach.
        await db.PowerPoints.Where(pp => pp.RoomId == roomGuid).LoadAsync(ct);
        var deviceIds = (await db.Devices.Where(d => d.PowerPoint.RoomId == roomGuid).ToListAsync(ct))
            .Select(d => d.DeviceId).ToList();
        await db.DeviceAssignmentPeriods.Where(p => deviceIds.Contains(p.DeviceId)).LoadAsync(ct);

        db.ApplyRowVersionCheck(room, rowVersion);
        db.Rooms.Remove(room);

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
