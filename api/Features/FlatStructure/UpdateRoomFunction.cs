using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EnergyTracker.Api.Features.FlatStructure;

public class UpdateRoomFunction(AppDbContext db, UpdateRoomRequestValidator validator)
{
    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    [Function("UpdateRoom")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/flats/{flatId}/rooms/{roomId}")]
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

        var room = await db.Rooms
            .Include(r => r.PowerPoints)
            .ThenInclude(pp => pp.Devices)
            .SingleOrDefaultAsync(r => r.RoomId == roomGuid && r.FlatId == flatGuid, ct);
        if (room is null)
            return new NotFoundObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                title = "Not Found", status = 404,
                detail = "Room not found."
            });

        UpdateRoomRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<UpdateRoomRequest>(req.Body, _jsonOptions, ct);
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

        var plugIds = request.PowerPoints
            .Select(pp => pp.PlugId).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        if (plugIds.Count != plugIds.Distinct().Count())
            return new ObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc4918#section-11.2",
                title = "Unprocessable Entity", status = 422,
                detail = "Each Smart Plug may be assigned to exactly one Power Point."
            }) { StatusCode = 422 };

        var powerPointIds = request.PowerPoints
            .Where(pp => pp.PowerPointId.HasValue).Select(pp => pp.PowerPointId!.Value).ToList();
        if (powerPointIds.Count != powerPointIds.Distinct().Count())
            return new ObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc4918#section-11.2",
                title = "Unprocessable Entity", status = 422,
                detail = "Each powerPointId may appear at most once in the request."
            }) { StatusCode = 422 };

        room.Name = request.Name.Trim();
        room.SortOrder = request.SortOrder;

        var existingPowerPointsById = room.PowerPoints.ToDictionary(pp => pp.PowerPointId);
        var matchedPowerPointIds = new HashSet<Guid>();
        var resultPowerPoints = new List<PowerPoint>();

        foreach (var ppInput in request.PowerPoints)
        {
            PowerPoint pp;
            if (ppInput.PowerPointId.HasValue
                && existingPowerPointsById.TryGetValue(ppInput.PowerPointId.Value, out var matchedPp))
            {
                pp = matchedPp;
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

            resultPowerPoints.Add(pp);
        }

        // Iterates the pre-mutation snapshot, not the live room.PowerPoints navigation — adding a
        // new PowerPoint above triggers EF's automatic relationship fixup (same RoomId), which would
        // otherwise re-appear here as an "unmatched" entry (its PowerPointId is still Guid.Empty,
        // not yet in matchedPowerPointIds) and get incorrectly removed before it's ever saved.
        foreach (var pp in existingPowerPointsById.Values)
        {
            if (!matchedPowerPointIds.Contains(pp.PowerPointId))
                db.PowerPoints.Remove(pp);
        }

        db.ApplyRowVersionCheck(room, request.RowVersion);

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

        // Matched, unchanged PowerPoints keep their as-loaded `.Devices` navigation (safe: this
        // endpoint never touches Devices), so it reflects current DB state without a re-fetch —
        // same reasoning as Story 13.1's Task 4 for the analogous UpdateDevice/UpdateFlatStructure case.
        var response = new RoomResponse(
            room.RoomId,
            room.Name,
            room.SortOrder,
            resultPowerPoints.Select(pp => new PowerPointResponse(
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
                .ToList(),
                pp.RowVersion))
            .ToList(),
            room.RowVersion);

        return new OkObjectResult(response);
    }
}
