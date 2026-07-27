using System.Text.Json;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Features.Insights;

public class StandbyDetector(AppDbContext db)
{
    // Neither Flat.cs nor any story adds a configurable usage window — hardcoded per Dev Notes.
    private const int UsageWindowStartHour = 22;
    private const int UsageWindowEndHour = 8;
    private const int OutOfUseHoursPerDay = 24 - UsageWindowStartHour + UsageWindowEndHour;

    // Eve Home export cadence is a documented fixed ~10-minute interval, not stored per-row.
    private const int IntervalMinutes = 10;
    private const decimal StandbyThresholdWatts = 2m;
    private const int MinDistinctDays = 7;
    private const int LookbackDays = 30;

    private record StandbyInsightData(string DeviceName, decimal MeanStandbyWatts, decimal EstimatedMonthlyKwh, decimal EstimatedMonthlyCost);

    public virtual async Task DetectAsync(Guid flatId, Guid runId, CancellationToken ct)
    {
        var rooms = await db.Rooms.AsNoTracking()
            .Include(r => r.PowerPoints).ThenInclude(pp => pp.Devices)
            .Where(r => r.FlatId == flatId)
            .ToListAsync(ct);

        var tariffs = await db.Tariffs.AsNoTracking()
            .Where(t => t.FlatId == flatId)
            .ToListAsync(ct);

        var cutoff = DateTimeOffset.UtcNow.AddDays(-LookbackDays);

        // Single-device attribution only — a smart strip's interval rows can't be isolated to
        // one device's watt draw (see Dev Notes).
        var eligible = rooms
            .SelectMany(r => r.PowerPoints)
            .Where(pp => pp.PlugId is not null && pp.Devices.Count == 1)
            .ToList();
        var plugIds = eligible.Select(pp => pp.PlugId!).ToList();

        var intervalRowsByPlugId = (await db.SmartPlugIntervalData.AsNoTracking()
                .Where(d => d.FlatId == flatId && plugIds.Contains(d.PlugId) && d.Timestamp >= cutoff)
                .ToListAsync(ct))
            .GroupBy(d => d.PlugId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var pp in eligible)
        {
            var device = pp.Devices.Single();

            // Zero interval rows means Meross-only (daily aggregates only) or no data at all —
            // neither is an error, both are silently excluded (AC #2).
            if (!intervalRowsByPlugId.TryGetValue(pp.PlugId!, out var intervalRows))
                continue;

            var distinctDays = intervalRows.Select(r => r.Timestamp.Date).Distinct().Count();
            if (distinctDays < MinDistinctDays)
                continue;

            var outOfUseRows = intervalRows
                .Where(r => r.Timestamp.Hour >= UsageWindowStartHour || r.Timestamp.Hour < UsageWindowEndHour)
                .ToList();
            if (outOfUseRows.Count == 0)
                continue;

            var meanWatts = outOfUseRows.Average(r => r.WhValue * (60m / IntervalMinutes));
            if (meanWatts <= StandbyThresholdWatts)
                continue;

            var tariff = TariffResolution.Resolve(tariffs, DateTimeOffset.UtcNow);
            if (tariff is null)
                continue;

            var estimatedMonthlyKwh = (meanWatts / 1000m) * OutOfUseHoursPerDay * 30m;
            var estimatedMonthlyCost = estimatedMonthlyKwh * tariff.PricePerKwh;

            if (await InsightDeduplication.IsNearDuplicateOfMostRecentAsync(db, flatId, InsightType.Standby, device.DeviceId, estimatedMonthlyCost, ct))
                continue;

            var data = new StandbyInsightData(device.Name, meanWatts, estimatedMonthlyKwh, estimatedMonthlyCost);
            db.Insights.Add(new Insight
            {
                InsightId = Guid.NewGuid(),
                FlatId = flatId,
                RunId = runId,
                Type = InsightType.Standby,
                DeviceId = device.DeviceId,
                Data = JsonSerializer.Serialize(data, InsightsConstants.MessageJsonOptions),
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
