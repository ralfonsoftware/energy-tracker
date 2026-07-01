using EnergyTracker.Api.Data.Entities;

namespace EnergyTracker.Api.Features.Dashboard;

public class KpiCalculator
{
    private const int MinCostDetailDays = 7;

    public DashboardSummary Compute(
        Flat flat,
        IReadOnlyList<MeterReading> readings,
        IReadOnlyList<Tariff> tariffs,
        DateTimeOffset now)
    {
        var dailyBudgetKwh = flat.AnnualKwhBaseline / 365m;

        if (readings.Count == 0)
            return new DashboardSummary(
                DailyAvgKwh: 0m, WeeklyAvgKwh: 0m,
                TodayKwh: 0m, DailyBudgetKwh: dailyBudgetKwh,
                LastReadingDate: null, SpikeDays: [], Cost: null);

        if (readings.Count == 1)
            return new DashboardSummary(
                DailyAvgKwh: 0m, WeeklyAvgKwh: 0m,
                TodayKwh: 0m, DailyBudgetKwh: dailyBudgetKwh,
                LastReadingDate: readings[0].ReadingDate, SpikeDays: [], Cost: null);

        // readings is pre-sorted ascending by ReadingDate
        var totalDays = (readings[^1].ReadingDate - readings[0].ReadingDate).TotalDays;
        // Sub-day spans (e.g. an accidental double-submit minutes apart) are floored to the
        // same zero-KPI path as totalDays<=0 — the app's reading cadence is daily at minimum.
        if (totalDays < 1.0)
            return new DashboardSummary(
                DailyAvgKwh: 0m, WeeklyAvgKwh: 0m,
                TodayKwh: 0m, DailyBudgetKwh: dailyBudgetKwh,
                LastReadingDate: readings[^1].ReadingDate, SpikeDays: [], Cost: null);

        // Single pass computes both total consumption and (when tariffed) total cost from the
        // same per-interval clamped deltas, so DailyAvgKwh and cost figures never diverge.
        decimal totalKwh = 0m;
        decimal totalCost = 0m;
        decimal coveredDays = 0m;
        for (var i = 0; i < readings.Count - 1; i++)
        {
            var periodKwh = Math.Max(0m, readings[i + 1].KwhValue - readings[i].KwhValue);
            totalKwh += periodKwh;

            if (tariffs.Count > 0)
            {
                var periodDays = (decimal)(readings[i + 1].ReadingDate - readings[i].ReadingDate).TotalDays;
                var tariff = ResolveTariff(tariffs, readings[i].ReadingDate);
                if (tariff is not null)
                {
                    totalCost += periodKwh * tariff.PricePerKwh;
                    coveredDays += periodDays;
                }
            }
        }

        var dailyAvgKwh = totalKwh / (decimal)totalDays;
        var weeklyAvgKwh = dailyAvgKwh * 7m;

        // TodayKwh: last interval's daily rate (most recent consumption trend); falls back to
        // the overall average for sub-day last intervals instead of dividing by a tiny fraction.
        var lastDays = (readings[^1].ReadingDate - readings[^2].ReadingDate).TotalDays;
        var lastKwh = Math.Max(0m, readings[^1].KwhValue - readings[^2].KwhValue);
        var todayKwh = lastDays >= 1.0 ? lastKwh / (decimal)lastDays : dailyAvgKwh;

        // Cost block: null when no tariff has ever been configured
        CostSummary? cost = null;
        if (tariffs.Count > 0)
        {
            var totalDaysInt = (int)Math.Ceiling(totalDays);
            var coveredDaysInt = Math.Min((int)Math.Ceiling(coveredDays), totalDaysInt);
            // dailyAvgCost divides by the same rounded CoveredDays reported to callers, so the
            // displayed denominator and the one used for the figure always agree.
            var dailyAvgCost = coveredDaysInt > 0 ? totalCost / coveredDaysInt : 0m;

            var currentTariff = ResolveTariff(tariffs, now);
            var projectedMonthlyCost = currentTariff is not null
                ? dailyAvgKwh * currentTariff.PricePerKwh * 30m
                : 0m;

            cost = new CostSummary(
                DailyAvgCost: dailyAvgCost,
                WeeklyAvgCost: dailyAvgCost * 7m,
                ProjectedMonthlyCost: projectedMonthlyCost,
                // Compares raw (unrounded) day counts so a genuine intra-day coverage gap isn't
                // masked by Math.Ceiling rounding both sides up to the same integer.
                HasCostGap: coveredDays < (decimal)totalDays,
                CoveredDays: coveredDaysInt,
                TotalDays: totalDaysInt,
                CostDetailAvailable: coveredDaysInt >= MinCostDetailDays
            );
        }

        return new DashboardSummary(
            DailyAvgKwh: dailyAvgKwh,
            WeeklyAvgKwh: weeklyAvgKwh,
            TodayKwh: todayKwh,
            DailyBudgetKwh: dailyBudgetKwh,
            LastReadingDate: readings[^1].ReadingDate,
            SpikeDays: [],
            Cost: cost
        );
    }

    private static Tariff? ResolveTariff(IReadOnlyList<Tariff> tariffs, DateTimeOffset date)
    {
        Tariff? best = null;
        foreach (var t in tariffs)
        {
            if (t.EffectiveDate <= date && (best is null || t.EffectiveDate > best.EffectiveDate))
                best = t;
        }
        return best;
    }
}
