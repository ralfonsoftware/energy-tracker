using EnergyTracker.Api.Data;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Features.FlatStructure;

public class GetFlatStructureFunction(AppDbContext db)
{
    [Function("GetFlatStructure")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flats/{flatId}/structure")]
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

        var flat = await db.Flats.AsNoTracking()
            .SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId, ct);
        if (flat is null)
            return new ObjectResult(new
            {
                title = "Forbidden", status = 403,
                detail = "Flat not found or access denied."
            }) { StatusCode = 403 };

        var rooms = await db.Rooms.AsNoTracking()
            .Include(r => r.PowerPoints)
            .ThenInclude(pp => pp.Devices)
            .Where(r => r.FlatId == flatGuid)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        var response = new FlatStructureResponse(
            flatGuid,
            HasDefaultTemplate: rooms.Count == 0,
            Rooms: rooms.Select(r => new RoomResponse(
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
            .ToList(),
            RowVersion: flat.RowVersion);

        return new OkObjectResult(response);
    }
}
