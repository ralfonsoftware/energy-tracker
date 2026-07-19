using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;

namespace EnergyTracker.Api.Features.Settings;

public class UpdateUserSettingsFunction(AppDbContext db, ILogger<UpdateUserSettingsFunction> logger)
{
    private static readonly string[] AllowedLocales = ["de-DE", "en-US"];

    [Function("UpdateUserSettings")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/user/settings")] HttpRequest req,
        FunctionContext context,
        CancellationToken ct)
    {
        using var reader = new StreamReader(req.Body);
        var body = await reader.ReadToEndAsync(ct);
        JsonNode? node = null;
        try
        {
            node = JsonNode.Parse(body, new JsonNodeOptions { PropertyNameCaseInsensitive = true });
        }
        catch (System.Text.Json.JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize request body in UpdateUserSettings");
        }

        if (node is not JsonObject obj)
        {
            return new BadRequestObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Bad Request",
                status = 400,
                detail = "Request body must be a JSON object."
            });
        }

        var locale = obj["locale"] is JsonValue localeVal && localeVal.TryGetValue<string>(out var localeStr) ? localeStr : null;
        if (locale is null || !AllowedLocales.Contains(locale))
        {
            return new BadRequestObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Bad Request",
                status = 400,
                detail = $"Locale must be one of: {string.Join(", ", AllowedLocales)}"
            });
        }

        var activeFlatIdProvided = obj.ContainsKey("activeFlatId");
        Guid? activeFlatId = null;
        if (activeFlatIdProvided)
        {
            var activeFlatIdNode = obj["activeFlatId"];
            if (activeFlatIdNode is JsonValue activeFlatIdVal && activeFlatIdVal.TryGetValue<string>(out var activeFlatIdStr))
            {
                if (!Guid.TryParse(activeFlatIdStr, out var parsedActiveFlatId))
                    return new BadRequestObjectResult(new
                    {
                        type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                        title = "Bad Request",
                        status = 400,
                        detail = "activeFlatId must be a valid GUID string or null."
                    });
                activeFlatId = parsedActiveFlatId;
            }
            else if (activeFlatIdNode is not null)
            {
                return new BadRequestObjectResult(new
                {
                    type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    title = "Bad Request",
                    status = 400,
                    detail = "activeFlatId must be a valid GUID string or null."
                });
            }
            // else: activeFlatIdNode is explicit JSON null -> activeFlatId stays null (clears the stored value)
        }

        var userId = context.GetUserId();

        if (activeFlatIdProvided && activeFlatId is not null)
        {
            var ownsFlat = await db.Flats.AnyAsync(f => f.FlatId == activeFlatId && f.UserId == userId, ct);
            if (!ownsFlat)
                return new ObjectResult(new
                {
                    title = "Forbidden",
                    status = 403,
                    detail = "activeFlatId does not belong to the resolved user."
                }) { StatusCode = 403 };
        }

        void ApplySettings(User target)
        {
            target.LocaleOverride = locale;
            if (activeFlatIdProvided)
                target.ActiveFlatId = activeFlatId;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null)
        {
            user = new User { UserId = userId };
            db.Users.Add(user);
        }
        ApplySettings(user);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            user = await db.Users.FirstAsync(u => u.UserId == userId, ct);
            ApplySettings(user);
            await db.SaveChangesAsync(ct);
        }

        Flat? flat = null;
        if (user.ActiveFlatId is Guid resolvedActiveFlatId)
            flat = await db.Flats.FirstOrDefaultAsync(f => f.FlatId == resolvedActiveFlatId && f.UserId == userId, ct);
        flat ??= await db.Flats.FirstOrDefaultAsync(f => f.UserId == userId, ct);
        var hasFlat = flat is not null;
        return new OkObjectResult(new UserSettingsResponse(
            locale,
            hasFlat,
            flat?.FlatId,
            flat?.Name,
            flat?.AnnualKwhBaseline,
            flat?.PlannedAnnualSpend,
            user.ActiveFlatId,
            flat?.RowVersion
        ));
    }
}
