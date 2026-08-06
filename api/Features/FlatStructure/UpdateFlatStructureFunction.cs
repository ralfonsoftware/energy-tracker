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

public class UpdateFlatStructureFunction(AppDbContext db, UpdateFlatStructureValidator validator)
{
    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

    [Function("UpdateFlatStructure")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/flats/{flatId}/structure")]
        HttpRequest req,
        string flatId,
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

        UpdateFlatStructureRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<UpdateFlatStructureRequest>(req.Body, _jsonOptions, ct);
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

        if (request.RowVersion is not { Length: > 0 })
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

        var plugIds = request.Rooms.SelectMany(r => r.PowerPoints)
            .Select(pp => pp.PlugId).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        if (plugIds.Count != plugIds.Distinct().Count())
            return new ObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc4918#section-11.2",
                title = "Unprocessable Entity", status = 422,
                detail = "Each Smart Plug may be assigned to exactly one Power Point."
            }) { StatusCode = 422 };

        var roomIds = request.Rooms.Where(r => r.RoomId.HasValue).Select(r => r.RoomId!.Value).ToList();
        var powerPointIds = request.Rooms.SelectMany(r => r.PowerPoints)
            .Where(pp => pp.PowerPointId.HasValue).Select(pp => pp.PowerPointId!.Value).ToList();
        if (roomIds.Count != roomIds.Distinct().Count()
            || powerPointIds.Count != powerPointIds.Distinct().Count())
            return new ObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc4918#section-11.2",
                title = "Unprocessable Entity", status = 422,
                detail = "Each roomId and powerPointId may appear at most once in the request."
            }) { StatusCode = 422 };

        // Devices are never touched by this request (see CreateDeviceFunction/UpdateDeviceFunction/
        // DeleteDeviceFunction), so this pre-mutation snapshot's PowerPoint.Devices navigation stays
        // accurate for the response built below.
        var existingRooms = await db.Rooms
            .Include(r => r.PowerPoints)
            .ThenInclude(pp => pp.Devices)
            .Where(r => r.FlatId == flatGuid)
            .ToListAsync(ct);

        var existingRoomsById = existingRooms.ToDictionary(r => r.RoomId);
        var existingPowerPointsById = existingRooms.SelectMany(r => r.PowerPoints)
            .ToDictionary(pp => pp.PowerPointId);

        var matchedRoomIds = new HashSet<Guid>();
        var matchedPowerPointIds = new HashSet<Guid>();
        var resultRooms = new List<Room>();
        // Tracks the final PowerPoints per Room explicitly, rather than relying on EF's as-loaded
        // navigation collection — that's a pre-mutation snapshot and would not reflect PowerPoints
        // added/moved above via db.PowerPoints.Add(...) rather than room.PowerPoints.Add(...).
        var powerPointsByRoom = new Dictionary<Room, List<PowerPoint>>();

        foreach (var roomInput in request.Rooms)
        {
            Room room;
            if (roomInput.RoomId.HasValue && existingRoomsById.TryGetValue(roomInput.RoomId.Value, out var matchedRoom))
            {
                room = matchedRoom;
                room.Name = roomInput.Name.Trim();
                room.SortOrder = roomInput.SortOrder;
                matchedRoomIds.Add(room.RoomId);
            }
            else
            {
                room = new Room { FlatId = flatGuid, Name = roomInput.Name.Trim(), SortOrder = roomInput.SortOrder };
                db.Rooms.Add(room);
            }

            var roomPowerPoints = new List<PowerPoint>();
            powerPointsByRoom[room] = roomPowerPoints;

            foreach (var ppInput in roomInput.PowerPoints)
            {
                PowerPoint pp;
                if (ppInput.PowerPointId.HasValue
                    && existingPowerPointsById.TryGetValue(ppInput.PowerPointId.Value, out var matchedPp))
                {
                    pp = matchedPp;
                    pp.RoomId = room.RoomId;
                    pp.Name = ppInput.Name.Trim();
                    pp.PlugId = ppInput.PlugId;
                    matchedPowerPointIds.Add(pp.PowerPointId);
                }
                else
                {
                    pp = new PowerPoint
                    {
                        RoomId = room.RoomId,
                        FlatId = flatGuid,
                        Name = ppInput.Name.Trim(),
                        PlugId = ppInput.PlugId
                    };
                    db.PowerPoints.Add(pp);
                }

                roomPowerPoints.Add(pp);
            }

            resultRooms.Add(room);
        }

        foreach (var room in existingRooms)
        {
            if (!matchedRoomIds.Contains(room.RoomId))
            {
                db.Rooms.Remove(room);
                continue;
            }

            foreach (var pp in room.PowerPoints.ToList())
            {
                if (!matchedPowerPointIds.Contains(pp.PowerPointId))
                    db.PowerPoints.Remove(pp);
            }
        }

        db.ApplyRowVersionCheck(flat, request.RowVersion);
        db.Entry(flat).State = EntityState.Modified;

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
        catch (DbUpdateException)
        {
            return new ConflictObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                title = "Conflict", status = 409,
                detail = "This Smart Plug is already assigned to another Power Point in this flat."
            });
        }

        var response = new FlatStructureResponse(
            flatGuid,
            HasDefaultTemplate: resultRooms.Count == 0,
            Rooms: resultRooms.OrderBy(r => r.SortOrder).Select(r => new RoomResponse(
                r.RoomId,
                r.Name,
                r.SortOrder,
                powerPointsByRoom[r].Select(pp => new PowerPointResponse(
                    pp.PowerPointId,
                    pp.Name,
                    pp.PlugId,
                    pp.Devices.Select(d => new DeviceResponse(
                        d.DeviceId,
                        d.Name,
                        d.Type,
                        d.Manufacturer,
                        d.Model,
                        d.PurchaseDate,
                        d.InUseSince,
                        d.DecommissionedDate,
                        d.ConsumptionApproach,
                        d.EuLabelClass,
                        d.EuAnnualKwh,
                        d.SelfMeasuredKwh,
                        d.SelfMeasuredPeriod,
                        d.RowVersion))
                    .ToList()))
                .ToList()))
            .ToList(),
            RowVersion: flat.RowVersion);

        return new OkObjectResult(response);
    }
}
