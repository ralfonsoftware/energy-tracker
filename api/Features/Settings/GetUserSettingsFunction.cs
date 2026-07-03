using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Features.Settings;

public class GetUserSettingsFunction(AppDbContext db, LocaleResolver localeResolver)
{
    [Function("GetUserSettings")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/user/settings")] HttpRequest req,
        FunctionContext context,
        CancellationToken ct)
    {
        var userId = context.GetUserId();

        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null)
        {
            user = new User { UserId = userId };
            db.Users.Add(user);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                user = await db.Users.FirstAsync(u => u.UserId == userId, ct);
            }
        }

        Flat? flat = null;
        if (user.ActiveFlatId is Guid activeFlatId)
            flat = await db.Flats.FirstOrDefaultAsync(f => f.FlatId == activeFlatId && f.UserId == userId, ct);
        flat ??= await db.Flats.FirstOrDefaultAsync(f => f.UserId == userId, ct);
        var hasFlat = flat is not null;

        return new OkObjectResult(new UserSettingsResponse(
            localeResolver.Resolve(req, user.LocaleOverride),
            hasFlat,
            flat?.FlatId,
            flat?.Name,
            flat?.AnnualKwhBaseline,
            flat?.PlannedAnnualSpend,
            user.ActiveFlatId
        ));
    }
}
