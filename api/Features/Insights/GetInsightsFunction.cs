using System.Text.Json;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnergyTracker.Api.Features.Insights;

public class GetInsightsFunction(AppDbContext db, ILogger<GetInsightsFunction> logger)
{
    [Function("GetInsights")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flats/{flatId}/insights")]
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

        var flat = await db.Flats.AsNoTracking()
            .SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId, ct);
        if (flat is null)
            return new ObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                title = "Forbidden", status = 403,
                detail = "Flat not found or access denied."
            }) { StatusCode = 403 };

        var mostRecentRun = await db.InsightRuns.AsNoTracking()
            .Where(r => r.FlatId == flatGuid)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync(ct);

        var runStatus = mostRecentRun is null
            ? null
            : new RunStatusDto(mostRecentRun.Status, mostRecentRun.StartedAt, mostRecentRun.CompletedAt);

        var status = req.Query["status"].ToString();
        bool wantDismissed;
        if (string.IsNullOrEmpty(status) || string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            wantDismissed = false;
        else if (string.Equals(status, "dismissed", StringComparison.OrdinalIgnoreCase))
            wantDismissed = true;
        else
            return new BadRequestObjectResult(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                title = "Bad Request", status = 400,
                detail = "status must be 'active' or 'dismissed'."
            });

        // Fetched unfiltered by IsDismissed: the per-identity selection below must pick each
        // identity's single true most-recent row first (regardless of dismissal state) before
        // routing by view, otherwise dismissing the current row could let an older, still-active
        // row for the same identity resurface in the Active view instead of the identity
        // disappearing entirely (AD-8c's whole-identity-suppression design).
        var insights = await db.Insights.AsNoTracking()
            .Where(i => i.FlatId == flatGuid)
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.InsightId)
            .Select(i => new { i.InsightId, i.Type, i.DeviceId, i.Data, i.CreatedAt, i.IsDismissed, i.RowVersion })
            .ToListAsync(ct);

        var insightsByIdentity = insights.GroupBy(i => (i.Type, i.DeviceId)).ToList();

        var insightDtos = new List<InsightDto>(insightsByIdentity.Count);
        foreach (var group in insightsByIdentity)
        {
            var candidates = group.ToList();
            InsightDto? selected = null;
            var selectedIsDismissed = false;

            foreach (var i in candidates)
            {
                JsonElement data;
                try
                {
                    // .Clone() detaches the element from the JsonDocument so it remains valid
                    // after the document is disposed — the response is serialized later in the
                    // pipeline, after this method has already returned.
                    using var doc = JsonDocument.Parse(i.Data);
                    data = doc.RootElement.Clone();
                }
                catch (JsonException ex)
                {
                    logger.LogError(ex, "Insight {InsightId} has malformed Data JSON; falling back to the next-newest row for this identity, if any.", i.InsightId);
                    continue;
                }

                selected = new InsightDto(i.InsightId, i.Type, i.DeviceId, data, i.CreatedAt, i.RowVersion);
                selectedIsDismissed = i.IsDismissed;
                break;
            }

            // The identity's current representative row belongs to exactly one view at a time —
            // if it doesn't match the requested view, the identity has nothing to show here (no
            // fallback to an older row of the other dismissal state).
            if (selected is null || selectedIsDismissed != wantDismissed)
                continue;

            insightDtos.Add(selected);

            if (candidates.Count > 1)
                logger.LogDebug("Identity ({Type}, device {DeviceId}) has {Count} historical rows; {InsightId} selected as most recent.", selected.Type, selected.DeviceId, candidates.Count, selected.InsightId);
        }

        return new OkObjectResult(new InsightsResponse(runStatus, insightDtos));
    }
}
