using EnergyTracker.Api.Data.Entities;

namespace EnergyTracker.Api.Features.Dashboard;

public class KpiCalculator
{
    private const int MinCostDetailDays = 7;

    // Single-market app (Germany) with no per-Flat/User timezone field today — day-bucketing for
    // the trend chart and spike detection must agree on one timezone, so both BuildDailySeries and
    // the window computation below convert through this fixed zone rather than mixing UTC and
    // each reading's own stored offset. Falls back to UTC if the runtime lacks IANA tzdata (e.g.
    // InvariantGlobalization or a minimal container image) so a missing timezone database doesn't
    // permanently fail every dashboard request for the process lifetime.
    private static readonly TimeZoneInfo AppTimeZone = ResolveAppTimeZone();

    private static TimeZoneInfo ResolveAppTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

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
                LastReadingDate: null, SpikeDays: [], Cost: null, LastKwhValue: null,
                DailyConsumption: []);

        if (readings.Count == 1)
            return new DashboardSummary(
                DailyAvgKwh: 0m, WeeklyAvgKwh: 0m,
                TodayKwh: 0m, DailyBudgetKwh: dailyBudgetKwh,
                LastReadingDate: readings[0].ReadingDate, SpikeDays: [], Cost: null,
                LastKwhValue: readings[0].KwhValue, DailyConsumption: []);

        // readings is pre-sorted ascending by ReadingDate
        var totalDays = (readings[^1].ReadingDate - readings[0].ReadingDate).TotalDays;
        // Sub-day spans (e.g. an accidental double-submit minutes apart) are floored to the
        // same zero-KPI path as totalDays<=0 — the app's reading cadence is daily at minimum.
        if (totalDays < 1.0)
            return new DashboardSummary(
                DailyAvgKwh: 0m, WeeklyAvgKwh: 0m,
                TodayKwh: 0m, DailyBudgetKwh: dailyBudgetKwh,
                LastReadingDate: readings[^1].ReadingDate, SpikeDays: [], Cost: null,
                LastKwhValue: readings[^1].KwhValue, DailyConsumption: []);

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

        var dailySeries = BuildDailySeries(readings);
        var windowEnd = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, AppTimeZone).Date);
        var windowStart = windowEnd.AddDays(-6);
        var dailyConsumption = new List<DailyConsumptionPoint>();
        for (var date = windowStart; date <= windowEnd; date = date.AddDays(1))
            dailyConsumption.Add(new DailyConsumptionPoint(date.ToString("yyyy-MM-dd"), dailySeries.GetValueOrDefault(date)));
        var spikeDays = DetectSpikes(dailySeries, windowStart, windowEnd, flat.SpikeThreshold);

        return new DashboardSummary(
            DailyAvgKwh: dailyAvgKwh,
            WeeklyAvgKwh: weeklyAvgKwh,
            TodayKwh: todayKwh,
            DailyBudgetKwh: dailyBudgetKwh,
            LastReadingDate: readings[^1].ReadingDate,
            SpikeDays: spikeDays,
            Cost: cost,
            LastKwhValue: readings[^1].KwhValue,
            DailyConsumption: dailyConsumption.ToArray()
        );
    }

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

    private static Dictionary<DateOnly, decimal> BuildDailySeries(IReadOnlyList<MeterReading> readings)
    {
        var series = new Dictionary<DateOnly, decimal>();
        for (var i = 0; i < readings.Count - 1; i++)
        {
            var start = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(readings[i].ReadingDate, AppTimeZone).Date);
            var end = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(readings[i + 1].ReadingDate, AppTimeZone).Date);
            var periodKwh = Math.Max(0m, readings[i + 1].KwhValue - readings[i].KwhValue);
            var spanDays = Math.Max(1, end.DayNumber - start.DayNumber);
            var perDayKwh = periodKwh / spanDays;
            // Two readings on the same calendar day: attribute the whole delta to that one day.
            var firstDay = end.DayNumber > start.DayNumber ? start.DayNumber + 1 : end.DayNumber;
            for (var d = firstDay; d <= end.DayNumber; d++)
            {
                var date = DateOnly.FromDayNumber(d);
                series[date] = series.GetValueOrDefault(date) + perDayKwh;
            }
        }
        return series;
    }

    private static string[] DetectSpikes(
        Dictionary<DateOnly, decimal> dailySeries, DateOnly windowStart, DateOnly windowEnd, decimal threshold)
    {
        var spikes = new List<string>();
        for (var date = windowStart; date <= windowEnd; date = date.AddDays(1))
        {
            var dayKwh = dailySeries.GetValueOrDefault(date);
            decimal rollingSum = 0m;
            var priorDaysWithData = 0;
            for (var lookback = 1; lookback <= 7; lookback++)
            {
                if (dailySeries.TryGetValue(date.AddDays(-lookback), out var priorKwh))
                {
                    rollingSum += priorKwh;
                    priorDaysWithData++;
                }
            }
            if (priorDaysWithData == 0 || threshold <= 0m) continue;
            var rollingAvg = rollingSum / priorDaysWithData;
            if (rollingAvg > 0m && dayKwh > threshold * rollingAvg)
                spikes.Add(date.ToString("yyyy-MM-dd"));
        }
        return spikes.ToArray();
    }
}
