using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EnergyTracker.Api.Features.Settings;

public class UpdateUserSettingsFunction(AppDbContext db, ILogger<UpdateUserSettingsFunction> logger)
{
    private static readonly string[] AllowedLocales = ["de-DE", "en-US"];
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Function("UpdateUserSettings")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/user/settings")] HttpRequest req,
        FunctionContext context,
        CancellationToken ct)
    {
        UpdateUserSettingsRequest? body = null;
        try
        {
            body = await JsonSerializer.DeserializeAsync<UpdateUserSettingsRequest>(req.Body, _jsonOptions, ct);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize request body in UpdateUserSettings");
        }

        if (body is null || !AllowedLocales.Contains(body.Locale))
        {
            return new BadRequestObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Bad Request",
                status = 400,
                detail = $"Locale must be one of: {string.Join(", ", AllowedLocales)}"
            });
        }

        var userId = context.GetUserId();
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null)
        {
            user = new User { UserId = userId, LocaleOverride = body.Locale };
            db.Users.Add(user);
        }
        else
        {
            user.LocaleOverride = body.Locale;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            user = await db.Users.FirstAsync(u => u.UserId == userId, ct);
            user.LocaleOverride = body.Locale;
            await db.SaveChangesAsync(ct);
        }

        var hasFlat = await db.Flats.AnyAsync(f => f.UserId == userId, ct);
        return new OkObjectResult(new UserSettingsResponse(user.LocaleOverride, hasFlat));
    }
}
