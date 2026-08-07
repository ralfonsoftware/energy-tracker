using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EnergyTracker.Api.Features.FlatStructure;

public class CreateRoomFunction(AppDbContext db, CreateRoomRequestValidator validator)
{
    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    [Function("CreateRoom")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/flats/{flatId}/rooms")]
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

        CreateRoomRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateRoomRequest>(req.Body, _jsonOptions, ct);
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

        var plugIds = request.PowerPoints
            .Select(pp => pp.PlugId).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        if (plugIds.Count != plugIds.Distinct().Count())
            return new ObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc4918#section-11.2",
                title = "Unprocessable Entity", status = 422,
                detail = "Each Smart Plug may be assigned to exactly one Power Point."
            }) { StatusCode = 422 };

        var room = new Room { FlatId = flatGuid, Name = request.Name.Trim(), SortOrder = request.SortOrder };
        db.Rooms.Add(room);

        var powerPoints = request.PowerPoints.Select(ppInput => new PowerPoint
        {
            RoomId = room.RoomId,
            FlatId = flatGuid,
            Name = ppInput.Name.Trim(),
            PlugId = ppInput.PlugId,
            Room = room
        }).ToList();
        db.PowerPoints.AddRange(powerPoints);

        try
        {
            await db.SaveChangesAsync(ct);
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

        var response = new RoomResponse(
            room.RoomId,
            room.Name,
            room.SortOrder,
            powerPoints.Select(pp => new PowerPointResponse(
                pp.PowerPointId,
                pp.Name,
                pp.PlugId,
                [],
                pp.RowVersion))
            .ToList(),
            room.RowVersion);

        return new CreatedResult($"/api/v1/flats/{flatId}/rooms/{room.RoomId}", response);
    }
}
