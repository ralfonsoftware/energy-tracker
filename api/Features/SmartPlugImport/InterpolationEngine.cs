using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnergyTracker.Api.Features.SmartPlugImport;

public record GapRange(DateOnly Start, DateOnly End);

public record InterpolationResult(DateOnly? PeriodStart, DateOnly? PeriodEnd, IReadOnlyList<GapRange> Gaps);

public class InterpolationEngine(AppDbContext db, ILogger<InterpolationEngine> logger)
{
    private const int LookbackDays = 7;

    public async Task<InterpolationResult> InterpolateGapsAsync(Guid flatId, string plugId, CancellationToken ct)
    {
        var existing = await db.SmartPlugDailyData
            .Where(d => d.FlatId == flatId && d.PlugId == plugId)
            .ToListAsync(ct);

        if (existing.Count == 0)
            return new InterpolationResult(null, null, []);

        var byDate = existing.ToDictionary(d => d.Date, d => d.KwhValue);
        var minDate = existing.Min(d => d.Date);
        var maxDate = existing.Max(d => d.Date);

        var gapRanges = new List<GapRange>();
        var newRows = new List<SmartPlugDailyData>();

        for (var date = minDate; date <= maxDate; date = date.AddDays(1))
        {
            if (byDate.ContainsKey(date))
                continue;

            var gapStart = date;
            var gapEnd = date;
            while (gapEnd.AddDays(1) <= maxDate && !byDate.ContainsKey(gapEnd.AddDays(1)))
                gapEnd = gapEnd.AddDays(1);

            var anchorBeforeDate = gapStart.AddDays(-1);
            var anchorAfterDate = gapEnd.AddDays(1);
            var anchorBeforeValue = byDate[anchorBeforeDate];
            var anchorAfterValue = byDate[anchorAfterDate];
            var span = anchorAfterDate.DayNumber - anchorBeforeDate.DayNumber;

            decimal sum = 0m;
            var count = 0;
            for (var d = anchorBeforeDate; d > anchorBeforeDate.AddDays(-LookbackDays); d = d.AddDays(-1))
            {
                if (byDate.TryGetValue(d, out var v))
                {
                    sum += v;
                    count++;
                }
            }
            var preGapAverage = sum / count;

            for (var missing = gapStart; missing <= gapEnd; missing = missing.AddDays(1))
            {
                var step = missing.DayNumber - anchorBeforeDate.DayNumber;
                var rawInterpolated = anchorBeforeValue + (anchorAfterValue - anchorBeforeValue) * step / span;
                var cappedValue = Math.Min(rawInterpolated, preGapAverage);

                newRows.Add(new SmartPlugDailyData
                {
                    FlatId = flatId,
                    PlugId = plugId,
                    Date = missing,
                    KwhValue = cappedValue,
                    IsInterpolated = true
                });
            }

            gapRanges.Add(new GapRange(gapStart, gapEnd));
            date = gapEnd;
        }

        if (newRows.Count == 0)
            return new InterpolationResult(minDate, maxDate, []);

        db.SmartPlugDailyData.AddRange(newRows);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex,
                "Concurrent import detected for plug {PlugId} while inserting interpolated rows: another import already committed a conflicting row. Skipping this invocation.",
                plugId);
            foreach (var row in newRows)
                db.Entry(row).State = EntityState.Detached;
            return new InterpolationResult(minDate, maxDate, []);
        }

        return new InterpolationResult(minDate, maxDate, gapRanges);
    }
}
