using EnergyTracker.Api.Data;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace EnergyTracker.Api.Features.Insights;

public class PatchInsightFunction(AppDbContext db)
{
    [Function("PatchInsight")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/flats/{flatId}/insights/{insightId}")] HttpRequest req,
        string flatId,
        string insightId,
        FunctionContext context,
        CancellationToken ct)
    {
        var userId = context.GetUserId();

        if (!Guid.TryParse(flatId, out var flatGuid))
            return new BadRequestObjectResult(new { type = "https://tools.ietf.org/html/rfc7231#section-6.5.1", title = "Bad Request", status = 400, detail = "Invalid flatId format." });

        var flat = await db.Flats.AsNoTracking().SingleOrDefaultAsync(f => f.FlatId == flatGuid, ct);
        if (flat is null || flat.UserId != userId)
            return new ObjectResult(new { type = "https://tools.ietf.org/html/rfc7231#section-6.5.3", title = "Forbidden", status = 403, detail = "Flat not found or access denied." }) { StatusCode = 403 };

        if (!Guid.TryParse(insightId, out var insightGuid))
            return new BadRequestObjectResult(new { type = "https://tools.ietf.org/html/rfc7231#section-6.5.1", title = "Bad Request", status = 400, detail = "Invalid insightId format." });

        var insight = await db.Insights.SingleOrDefaultAsync(i => i.InsightId == insightGuid && i.FlatId == flatGuid, ct);
        if (insight is null)
            return new NotFoundObjectResult(new { type = "https://tools.ietf.org/html/rfc7231#section-6.5.4", title = "Not Found", status = 404, detail = "Insight not found." });

        using var reader = new StreamReader(req.Body);
        var body = await reader.ReadToEndAsync(ct);
        JsonNode? node = null;
        try { node = JsonNode.Parse(body); } catch (System.Text.Json.JsonException) { }

        if (node is not JsonObject obj)
            return new BadRequestObjectResult(new { type = "https://tools.ietf.org/html/rfc7231#section-6.5.1", title = "Bad Request", status = 400, detail = "Request body must be a JSON object." });

        if (obj["isDismissed"] is not JsonValue isDismissedVal || !isDismissedVal.TryGetValue<bool>(out var isDismissed))
            return new BadRequestObjectResult(new { type = "https://tools.ietf.org/html/rfc7231#section-6.5.1", title = "Bad Request", status = 400, detail = "isDismissed is required and must be a boolean." });

        var rowVersionStr = obj["rowVersion"] is JsonValue rowVersionVal && rowVersionVal.TryGetValue<string>(out var rvs) ? rvs : null;
        if (!ConcurrencyExtensions.TryParseRowVersion(rowVersionStr, out var rowVersion))
            return new BadRequestObjectResult(new { type = "https://tools.ietf.org/html/rfc7231#section-6.5.1", title = "Bad Request", status = 400, detail = "rowVersion is required." });

        if (isDismissed)
        {
            insight.IsDismissed = true;
            insight.DismissedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            insight.IsDismissed = false;
            insight.DismissedAt = null;
        }

        db.ApplyRowVersionCheck(insight, rowVersion);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ObjectResult(new { type = "https://tools.ietf.org/html/rfc9110#section-15.5.10", title = "Conflict", status = 409, detail = "This record was modified by another request. Reload and try again." }) { StatusCode = 409 };
        }

        return new OkObjectResult(new PatchInsightResponse(insight.InsightId, insight.IsDismissed, insight.DismissedAt, insight.RowVersion));
    }
}
