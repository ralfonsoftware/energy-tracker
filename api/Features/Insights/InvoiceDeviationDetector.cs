using System.Text.Json;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Features.Insights;

public class InvoiceDeviationDetector(AppDbContext db)
{
    private const int WindowDays = 60;

    // Raw fraction (e.g. 0.10m == 10%) — compared against `deviation`, never against the
    // `deviationPct` (x100) value ultimately written to Insight.Data.
    private const decimal DeviationThreshold = 0.10m;

    private record InvoiceDeviationInsightData(
        decimal ProjectedAnnualKwh, decimal BaselineKwh, decimal DeviationPct, decimal ImpliedDeltaEur, string Direction);

    public virtual async Task DetectAsync(Guid flatId, Guid runId, CancellationToken ct)
    {
        var flat = await db.Flats.AsNoTracking().SingleOrDefaultAsync(f => f.FlatId == flatId, ct);
        if (flat is null)
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

        decimal totalKwh = 0m;
        for (var i = 0; i < window.Count - 1; i++)
            totalKwh += Math.Max(0m, window[i + 1].KwhValue - window[i].KwhValue);

        var actualWindowDays = (decimal)(window[^1].ReadingDate - window[0].ReadingDate).TotalDays;
        // Sub-day spans (e.g. two readings minutes apart) are floored to a skip, same as
        // KpiCalculator.Compute's totalDays<1.0 guard — the app's reading cadence is daily at minimum.
        if (actualWindowDays < 1.0m)
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        var dailyAverageKwh = totalKwh / actualWindowDays;
        var projectedAnnualKwh = dailyAverageKwh * 365m;

        // AnnualKwhBaseline is non-nullable and validated GreaterThan(0) at every write path
        // (Flat.cs, FlatConfiguration.cs) — no zero-guard needed here.
        var baselineKwh = flat.AnnualKwhBaseline;
        var deviation = Math.Abs(projectedAnnualKwh - baselineKwh) / baselineKwh;

        if (deviation < DeviationThreshold)
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        var tariffs = await db.Tariffs.AsNoTracking().Where(t => t.FlatId == flatId).ToListAsync(ct);
        var tariff = TariffResolution.Resolve(tariffs, DateTimeOffset.UtcNow);
        if (tariff is null)
        {
            await db.SaveChangesAsync(ct);
            return;
        }

        var direction = projectedAnnualKwh > baselineKwh ? "above" : "below";
        var data = new InvoiceDeviationInsightData(
            projectedAnnualKwh, baselineKwh, deviation * 100m, (projectedAnnualKwh - baselineKwh) * tariff.PricePerKwh, direction);

        db.Insights.Add(new Insight
        {
            InsightId = Guid.NewGuid(),
            FlatId = flatId,
            RunId = runId,
            Type = InsightType.InvoiceDeviation,
            DeviceId = null,
            Data = JsonSerializer.Serialize(data, InsightsConstants.MessageJsonOptions),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);
    }

    // Anchor-based rolling window — duplicated verbatim from BudgetAlertDetector per this
    // codebase's established per-detector duplication convention (see that file for the algorithm
    // rationale, including the actualWindowDays < 1.0m floor guard in the caller).
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
}
