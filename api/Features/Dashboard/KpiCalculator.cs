using EnergyTracker.Api.Data.Entities;

namespace EnergyTracker.Api.Features.Dashboard;

public class KpiCalculator
{
    public DashboardSummary Compute(
        Flat flat,
        IReadOnlyList<MeterReading> readings,
        IReadOnlyList<Tariff> tariffs,
        DateTimeOffset now)
    {
        var dailyBudgetKwh = flat.AnnualKwhBaseline / 365m;

        if (readings.Count == 0)
            return new DashboardSummary(0m, 0m, 0m, 0m, 0m, null, 0m, dailyBudgetKwh, []);

        if (readings.Count == 1)
            return new DashboardSummary(0m, 0m, 0m, 0m, 0m, readings[0].ReadingDate, 0m, dailyBudgetKwh, []);

        // readings is pre-sorted ascending by ReadingDate
        var totalDays = (readings[^1].ReadingDate - readings[0].ReadingDate).TotalDays;
        if (totalDays <= 0)
            return new DashboardSummary(0m, 0m, 0m, 0m, 0m, readings[^1].ReadingDate, 0m, dailyBudgetKwh, []);

        var totalKwh = Math.Max(0m, readings[^1].KwhValue - readings[0].KwhValue);
        var dailyAvgKwh = totalKwh / (decimal)totalDays;
        var weeklyAvgKwh = dailyAvgKwh * 7m;

        // Period-accurate cost: each inter-reading interval uses tariff active at interval start
        decimal totalCost = 0m;
        for (var i = 0; i < readings.Count - 1; i++)
        {
            var periodKwh = Math.Max(0m, readings[i + 1].KwhValue - readings[i].KwhValue);
            var tariff = ResolveTariff(tariffs, readings[i].ReadingDate);
            if (tariff is not null)
                totalCost += periodKwh * tariff.PricePerKwh;
        }

        var dailyAvgCost = totalCost / (decimal)totalDays;
        var weeklyAvgCost = dailyAvgCost * 7m;

        // ProjectedMonthlyCost: daily avg kWh × current tariff price × 30 days
        var currentTariff = ResolveTariff(tariffs, now);
        var projectedMonthlyCost = currentTariff is not null
            ? dailyAvgKwh * currentTariff.PricePerKwh * 30m
            : 0m;

        // TodayKwh: last interval's daily rate (most recent consumption trend)
        var lastDays = (readings[^1].ReadingDate - readings[^2].ReadingDate).TotalDays;
        var lastKwh = Math.Max(0m, readings[^1].KwhValue - readings[^2].KwhValue);
        var todayKwh = lastDays > 0 ? lastKwh / (decimal)lastDays : dailyAvgKwh;

        return new DashboardSummary(
            DailyAvgKwh: dailyAvgKwh,
            WeeklyAvgKwh: weeklyAvgKwh,
            DailyAvgCost: dailyAvgCost,
            WeeklyAvgCost: weeklyAvgCost,
            ProjectedMonthlyCost: projectedMonthlyCost,
            LastReadingDate: readings[^1].ReadingDate,
            TodayKwh: todayKwh,
            DailyBudgetKwh: dailyBudgetKwh,
            SpikeDays: []
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
