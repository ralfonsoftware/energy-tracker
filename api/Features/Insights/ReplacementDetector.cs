using System.Text.Json;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Features.Insights;

public class ReplacementDetector(AppDbContext db)
{
    private const int MinDistinctDays = 7;
    private const int LookbackDays = 30;
    private const decimal TopBandFraction = 0.2m;

    // Story-creation-time product decision — no per-device-category wattage-by-class
    // reference table exists in this system (see Dev Notes).
    private const decimal SavingsPerClassStepPercent = 0.15m;

    // Best-to-worst ordered EU energy-label scale, both legacy (pre-2021) and modern grades.
    // Index = rank; "C or below" = rank >= IndexOf("C").
    private static readonly string[] EuLabelScale = ["A+++", "A++", "A+", "A", "B", "C", "D", "E", "F", "G"];
    private static readonly int WorstAcceptableRank = Array.IndexOf(EuLabelScale, "C");

    private record ReplacementInsightData(
        string DeviceName, decimal EstimatedAnnualKwh, decimal EstimatedAnnualCost, string SuggestedClass, decimal EstimatedSavingsEur);

    public virtual async Task DetectAsync(Guid flatId, Guid runId, CancellationToken ct)
    {
        var rooms = await db.Rooms.AsNoTracking()
            .Include(r => r.PowerPoints).ThenInclude(pp => pp.Devices)
            .Where(r => r.FlatId == flatId)
            .ToListAsync(ct);

        var tariffs = await db.Tariffs.AsNoTracking()
            .Where(t => t.FlatId == flatId)
            .ToListAsync(ct);

        var tariff = TariffResolution.Resolve(tariffs, DateTimeOffset.UtcNow);
        if (tariff is null)
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        var powerPoints = rooms.SelectMany(r => r.PowerPoints).ToList();

        // Single-device attribution mirrors StandbyDetector/DecompositionEngine's Measured
        // branch — smart strips can't isolate a per-device annual figure this way.
        var measuredPlugIds = powerPoints
            .Where(pp => pp.PlugId is not null && pp.Devices.Count == 1)
            .Select(pp => pp.PlugId!)
            .ToList();

        var cutoffDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-LookbackDays));
        var dailyRowsByPlugId = (await db.SmartPlugDailyData.AsNoTracking()
                .Where(d => d.FlatId == flatId && measuredPlugIds.Contains(d.PlugId) && d.Date >= cutoffDate)
                .ToListAsync(ct))
            .GroupBy(d => d.PlugId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var candidates = new List<(Device Device, decimal AnnualKwh, decimal AnnualCost)>();

        foreach (var pp in powerPoints)
        {
            var isSingleDeviceMeasured = pp.PlugId is not null && pp.Devices.Count == 1;

            foreach (var device in pp.Devices)
            {
                var annualKwh = isSingleDeviceMeasured
                    ? ComputeMeasuredAnnualKwh(dailyRowsByPlugId, pp.PlugId!) ?? ComputeApproachAnnualKwh(device)
                    : ComputeApproachAnnualKwh(device);

                if (annualKwh is null)
                    continue;

                candidates.Add((device, annualKwh.Value, annualKwh.Value * tariff.PricePerKwh));
            }
        }

        var bandSize = Math.Max(1, (int)Math.Ceiling(candidates.Count * TopBandFraction));
        var topBand = candidates
            .OrderByDescending(c => c.AnnualCost)
            .ThenBy(c => c.Device.DeviceId)
            .Take(bandSize);

        foreach (var candidate in topBand)
        {
            var rank = NormalizeEuLabelClass(candidate.Device.EuLabelClass);
            if (rank is null || rank < WorstAcceptableRank)
                continue;

            var suggestedClass = EuLabelScale[rank.Value - 1];
            var estimatedSavingsEur = candidate.AnnualCost * SavingsPerClassStepPercent;

            var data = new ReplacementInsightData(
                candidate.Device.Name, candidate.AnnualKwh, candidate.AnnualCost, suggestedClass, estimatedSavingsEur);

            db.Insights.Add(new Insight
            {
                InsightId = Guid.NewGuid(),
                FlatId = flatId,
                RunId = runId,
                Type = InsightType.Replacement,
                DeviceId = candidate.Device.DeviceId,
                Data = JsonSerializer.Serialize(data, InsightsConstants.MessageJsonOptions),
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static decimal? ComputeMeasuredAnnualKwh(IReadOnlyDictionary<string, List<SmartPlugDailyData>> dailyRowsByPlugId, string plugId)
    {
        if (!dailyRowsByPlugId.TryGetValue(plugId, out var rows))
            return null;

        var dailyByDate = rows.GroupBy(r => r.Date).ToDictionary(g => g.Key, g => g.Last().KwhValue);
        if (dailyByDate.Count < MinDistinctDays)
            return null;

        return dailyByDate.Values.Average() * 365m;
    }

    private static decimal? ComputeApproachAnnualKwh(Device device) => device.ConsumptionApproach switch
    {
        ConsumptionApproach.EuLabel => device.EuAnnualKwh,
        ConsumptionApproach.SelfMeasured => device.SelfMeasuredKwh is decimal kwh
            ? kwh * (device.SelfMeasuredPeriod == SelfMeasuredPeriod.Weekly ? 52m : 365m)
            : null,
        _ => null
    };

    private static int? NormalizeEuLabelClass(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var index = Array.IndexOf(EuLabelScale, raw.Trim().ToUpperInvariant());
        return index >= 0 ? index : null;
    }
}
