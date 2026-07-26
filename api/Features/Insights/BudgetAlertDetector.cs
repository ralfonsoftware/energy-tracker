using System.Text.Json;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Features.Insights;

public class BudgetAlertDetector(AppDbContext db)
{
    private const int WindowDays = 30;

    private record BudgetInsightData(decimal ProjectedAnnualCost, decimal PlannedAnnualSpend, decimal OverspendEur);

    public virtual async Task DetectAsync(Guid flatId, Guid runId, CancellationToken ct)
    {
        var flat = await db.Flats.AsNoTracking().SingleOrDefaultAsync(f => f.FlatId == flatId, ct);
        if (flat is null || flat.PlannedAnnualSpend is null)
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        var readings = await db.MeterReadings.AsNoTracking()
            .Where(r => r.FlatId == flatId)
            .OrderBy(r => r.ReadingDate)
            .ToListAsync(ct);

        var window = ResolveWindow(readings, WindowDays, DateTimeOffset.UtcNow);
        if (window is null)
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        var tariffs = await db.Tariffs.AsNoTracking().Where(t => t.FlatId == flatId).ToListAsync(ct);

        // No uncoveredDays bookkeeping (unlike KpiCalculator.Compute) — this detector has no UI
        // surface for a cost-gap flag; periods with no resolvable tariff simply contribute 0.
        decimal totalCost = 0m;
        for (var i = 0; i < window.Count - 1; i++)
        {
            var periodKwh = Math.Max(0m, window[i + 1].KwhValue - window[i].KwhValue);
            var tariff = ResolveTariff(tariffs, window[i].ReadingDate);
            if (tariff is not null)
                totalCost += periodKwh * tariff.PricePerKwh;
        }

        var actualWindowDays = (decimal)(window[^1].ReadingDate - window[0].ReadingDate).TotalDays;
        // Sub-day spans (e.g. two readings minutes apart) are floored to a skip, same as
        // KpiCalculator.Compute's totalDays<1.0 guard — the app's reading cadence is daily at minimum.
        if (actualWindowDays < 1.0m)
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        var dailyAverageCost = totalCost / actualWindowDays;
        var projectedAnnualCost = dailyAverageCost * 365m;

        if (projectedAnnualCost > flat.PlannedAnnualSpend.Value)
        {
            var data = new BudgetInsightData(
                projectedAnnualCost, flat.PlannedAnnualSpend.Value, projectedAnnualCost - flat.PlannedAnnualSpend.Value);

            db.Insights.Add(new Insight
            {
                InsightId = Guid.NewGuid(),
                FlatId = flatId,
                RunId = runId,
                Type = InsightType.Budget,
                DeviceId = null,
                Data = JsonSerializer.Serialize(data, InsightsConstants.MessageJsonOptions),
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }

    // Anchor-based rolling window: the last reading at/before "now - windowDays" proves at least
    // windowDays of history exists before the window; everything from there onward (inclusive) is
    // the window itself. This only guarantees the window's actual span is >= windowDays when the
    // latest ingested reading coincides with "now" — a lagging latest reading (or a sub-day cluster
    // of readings) can yield a shorter or near-zero span, which the caller floors via actualWindowDays
    // < 1.0m. Duplicated verbatim in InvoiceDeviationDetector per this codebase's established
    // per-detector duplication convention.
    private static List<MeterReading>? ResolveWindow(IReadOnlyList<MeterReading> readings, int windowDays, DateTimeOffset now)
    {
        var cutoff = now.AddDays(-windowDays);
        var anchorIndex = -1;
        for (var i = 0; i < readings.Count; i++)
        {
            if (readings[i].ReadingDate <= cutoff)
                anchorIndex = i;
            else
                break;
        }

        if (anchorIndex < 0)
            return null;

        var window = readings.Skip(anchorIndex).ToList();
        return window.Count < 2 ? null : window;
    }

    // Duplicated verbatim from KpiCalculator.cs/DecompositionEngine.cs per this codebase's
    // established per-engine duplication convention — TariffResolver was deleted, don't recreate it.
    private static Tariff? ResolveTariff(IReadOnlyList<Tariff> tariffs, DateTimeOffset date)
    {
        Tariff? best = null;
        foreach (var t in tariffs)
        {
            if (t.ContractStartDate <= date && (best is null || t.ContractStartDate > best.ContractStartDate))
                best = t;
        }
        return best;
    }
}
