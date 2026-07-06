using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnergyTracker.Api.Features.SmartPlugImport;

public class ReconciliationEngine(AppDbContext db, ILogger<ReconciliationEngine> logger)
{
    private const decimal CleanTolerance = 0.1m;
    private const decimal InterpolatedTolerance = 1.0m;

    // Mirrors KpiCalculator.AppTimeZone/ResolveAppTimeZone — no shared timezone-resolution
    // utility exists today, and each Function slice in this codebase is deliberately
    // self-contained (see Dev Notes in the story for this engine).
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

    public async Task ReconcileAsync(Guid flatId, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct)
    {
        var periodRows = await db.SmartPlugDailyData
            .Where(d => d.FlatId == flatId && d.Date >= periodStart && d.Date <= periodEnd)
            .ToListAsync(ct);

        var attributedKwh = periodRows.Sum(d => d.KwhValue);
        var hasInterpolatedData = periodRows.Any(d => d.IsInterpolated);

        var readings = await db.MeterReadings
            .Where(r => r.FlatId == flatId)
            .OrderBy(r => r.ReadingDate)
            .ToListAsync(ct);

        var mainMeterTotal = TryComputeMainMeterTotal(readings, periodStart, periodEnd);
        if (mainMeterTotal is null)
        {
            logger.LogInformation(
                "Skipping reconciliation for flat {FlatId}, period {PeriodStart}-{PeriodEnd}: insufficient meter reading coverage.",
                flatId, periodStart, periodEnd);
            return;
        }

        var residual = mainMeterTotal.Value - attributedKwh;
        var tolerance = hasInterpolatedData ? InterpolatedTolerance : CleanTolerance;

        if (residual < -tolerance)
        {
            throw new OverAttributionException(
                $"Attributed consumption ({attributedKwh} kWh) exceeds main meter total ({mainMeterTotal.Value} kWh) for period {periodStart}-{periodEnd} beyond the {tolerance} kWh tolerance.",
                attributedKwh, mainMeterTotal.Value, tolerance);
        }
    }

    private static decimal? TryComputeMainMeterTotal(List<MeterReading> readings, DateOnly periodStart, DateOnly periodEnd)
    {
        if (readings.Count < 2)
            return null;

        var firstLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(readings[0].ReadingDate, AppTimeZone).Date);
        var lastLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(readings[^1].ReadingDate, AppTimeZone).Date);

        if (periodStart <= firstLocalDate || periodEnd > lastLocalDate)
            return null;

        var series = BuildMainMeterDailySeries(readings);

        decimal total = 0m;
        for (var date = periodStart; date <= periodEnd; date = date.AddDays(1))
            total += series.GetValueOrDefault(date);

        return total;
    }

    private static Dictionary<DateOnly, decimal> BuildMainMeterDailySeries(List<MeterReading> readings)
    {
        var series = new Dictionary<DateOnly, decimal>();
        for (var i = 0; i < readings.Count - 1; i++)
        {
            var start = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(readings[i].ReadingDate, AppTimeZone).Date);
            var end = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(readings[i + 1].ReadingDate, AppTimeZone).Date);
            var periodKwh = Math.Max(0m, readings[i + 1].KwhValue - readings[i].KwhValue);
            var spanDays = Math.Max(1, end.DayNumber - start.DayNumber);
            var perDayKwh = periodKwh / spanDays;

            var firstDay = end.DayNumber > start.DayNumber ? start.DayNumber + 1 : end.DayNumber;
            for (var d = firstDay; d <= end.DayNumber; d++)
            {
                var date = DateOnly.FromDayNumber(d);
                series[date] = series.GetValueOrDefault(date) + perDayKwh;
            }
        }
        return series;
    }
}
