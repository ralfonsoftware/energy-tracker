using EnergyTracker.Api.Data;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Features.Flats;

public class DeleteFlatFunction(AppDbContext db)
{
    [Function("DeleteFlat")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/flats/{flatId}")] HttpRequest req,
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

        await db.MeterReadings.Where(r => r.FlatId == flatGuid).LoadAsync(ct);
        await db.Tariffs.Where(t => t.FlatId == flatGuid).LoadAsync(ct);
        await db.Rooms.Where(r => r.FlatId == flatGuid).LoadAsync(ct);
        await db.LoadPowerPointsAndDevicesAsync(flatGuid, ct);

        db.Flats.Remove(flat);
        await db.SaveChangesAsync(ct);

        return new NoContentResult();
    }
}
