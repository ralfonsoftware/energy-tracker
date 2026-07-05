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

        UpdateFlatStructureRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<UpdateFlatStructureRequest>(req.Body, _jsonOptions, ct);
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

        var plugIds = request.Rooms.SelectMany(r => r.PowerPoints)
            .Select(pp => pp.PlugId).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        if (plugIds.Count != plugIds.Distinct().Count())
            return new ObjectResult(new
            {
                title = "Unprocessable Entity", status = 422,
                detail = "Each Smart Plug may be assigned to exactly one Power Point."
            }) { StatusCode = 422 };

        var existingRooms = await db.Rooms.Where(r => r.FlatId == flatGuid).ToListAsync(ct);
        await db.LoadPowerPointsAndDevicesAsync(flatGuid, ct);
        db.Rooms.RemoveRange(existingRooms);

        var newRooms = request.Rooms.Select(r => new Room
        {
            FlatId = flatGuid,
            Name = r.Name.Trim(),
            SortOrder = r.SortOrder,
            PowerPoints = r.PowerPoints.Select(pp => new PowerPoint
            {
                Name = pp.Name.Trim(),
                PlugId = pp.PlugId,
                Devices = pp.Devices.Select(d => new Device
                {
                    Name = d.Name.Trim(),
                    Type = d.Type,
                    Manufacturer = d.Manufacturer,
                    Model = d.Model,
                    PurchaseDate = d.PurchaseDate,
                    ConsumptionApproach = d.ConsumptionApproach,
                    EuLabelClass = d.EuLabelClass,
                    EuAnnualKwh = d.EuAnnualKwh,
                    SelfMeasuredKwh = d.SelfMeasuredKwh,
                    SelfMeasuredPeriod = d.SelfMeasuredPeriod
                }).ToList()
            }).ToList()
        }).ToList();

        db.Rooms.AddRange(newRooms);
        await db.SaveChangesAsync(ct);

        var response = new FlatStructureResponse(
            flatGuid,
            HasDefaultTemplate: newRooms.Count == 0,
            Rooms: newRooms.OrderBy(r => r.SortOrder).Select(r => new RoomResponse(
                r.RoomId,
                r.Name,
                r.SortOrder,
                r.PowerPoints.Select(pp => new PowerPointResponse(
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
                        d.ConsumptionApproach,
                        d.EuLabelClass,
                        d.EuAnnualKwh,
                        d.SelfMeasuredKwh,
                        d.SelfMeasuredPeriod))
                    .ToList()))
                .ToList()))
            .ToList());

        return new OkObjectResult(response);
    }
}
