using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Features.Decomposition;

public class DecompositionEngine(AppDbContext db)
{
    // Mirrors ReconciliationEngine.AppTimeZone/ResolveAppTimeZone — no shared timezone-resolution
    // utility exists today; each Function slice in this codebase is deliberately self-contained.
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

    public async Task<DecompositionResponse> ComputeAsync(Guid flatId, DateOnly startDate, DateOnly endDate, CancellationToken ct)
    {
        var dailyRows = await db.SmartPlugDailyData
            .Where(d => d.FlatId == flatId && d.Date >= startDate && d.Date <= endDate)
            .ToListAsync(ct);

        if (dailyRows.Count == 0)
        {
            return new DecompositionResponse(
                new PeriodRange(startDate, endDate),
                TotalKwh: 0m, TotalCost: 0m,
                IsUnavailable: true, HasInterpolatedData: false,
                Residual: new ResidualItem(0m, 0m),
                Rooms: []);
        }

        var hasInterpolatedData = dailyRows.Any(d => d.IsInterpolated);

        var plugDailySeries = dailyRows
            .GroupBy(d => d.PlugId)
            .ToDictionary(g => g.Key, g => g
                .GroupBy(d => d.Date)
                .ToDictionary(dg => dg.Key, dg => dg.Last().KwhValue));

        var rooms = await db.Rooms.AsNoTracking()
            .Include(r => r.PowerPoints).ThenInclude(pp => pp.Devices)
            .Where(r => r.FlatId == flatId)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        var tariffs = await db.Tariffs.AsNoTracking()
            .Where(t => t.FlatId == flatId)
            .OrderBy(t => t.ContractStartDate)
            .ToListAsync(ct);

        var dayCount = endDate.DayNumber - startDate.DayNumber + 1;

        decimal CostForDailySeries(Func<DateOnly, decimal> dailyKwh)
        {
            decimal cost = 0m;
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var tariff = ResolveTariff(tariffs, ToLocalMidnight(date));
                if (tariff is not null)
                    cost += dailyKwh(date) * tariff.PricePerKwh;
            }
            return cost;
        }

        var roomDecompositions = new List<RoomDecomposition>();
        // A PowerPoint with a PlugId but zero attached Devices has no DeviceDecomposition to
        // attribute its measured kWh to (AC12, deferred to Story 7.3's UX). Track it separately so
        // the AC10 fallback branch below can still account for it in TotalKwh instead of losing it.
        var orphanedPlugSeries = new Dictionary<DateOnly, decimal>();

        foreach (var room in rooms)
        {
            var deviceDecompositions = new List<DeviceDecomposition>();

            foreach (var pp in room.PowerPoints)
            {
                if (pp.PlugId is not null && pp.Devices.Count == 1)
                {
                    var device = pp.Devices.Single();
                    var series = plugDailySeries.GetValueOrDefault(pp.PlugId, []);
                    var kwh = series.Values.Sum();
                    var cost = CostForDailySeries(date => series.GetValueOrDefault(date));
                    deviceDecompositions.Add(new DeviceDecomposition(
                        device.DeviceId, device.Name, kwh, cost,
                        AttributionApproach.Measured, IsSmartStrip: false, SubDevices: null));
                }
                else if (pp.PlugId is not null && pp.Devices.Count > 1)
                {
                    deviceDecompositions.Add(BuildSmartStripDecomposition(pp, plugDailySeries, dayCount, CostForDailySeries));
                }
                else if (pp.PlugId is not null && pp.Devices.Count == 0)
                {
                    var series = plugDailySeries.GetValueOrDefault(pp.PlugId, []);
                    foreach (var (date, kwh) in series)
                        orphanedPlugSeries[date] = orphanedPlugSeries.GetValueOrDefault(date) + kwh;
                }
                else
                {
                    foreach (var device in pp.Devices)
                    {
                        var (approach, dailyEstimate) = ResolveStandaloneApproach(device);
                        var kwh = dailyEstimate * dayCount;
                        var cost = approach == AttributionApproach.None ? 0m : CostForDailySeries(_ => dailyEstimate);
                        deviceDecompositions.Add(new DeviceDecomposition(
                            device.DeviceId, device.Name, kwh, cost, approach, IsSmartStrip: false, SubDevices: null));
                    }
                }
            }

            var roomKwh = deviceDecompositions.Sum(d => d.Kwh);
            var roomCost = deviceDecompositions.Sum(d => d.Cost);
            roomDecompositions.Add(new RoomDecomposition(room.RoomId, room.Name, roomKwh, roomCost, deviceDecompositions));
        }

        var allDeviceKwh = roomDecompositions.Sum(r => r.Kwh);
        var allDeviceCost = roomDecompositions.Sum(r => r.Cost);
        var orphanedPlugKwh = orphanedPlugSeries.Values.Sum();
        var orphanedPlugCost = CostForDailySeries(date => orphanedPlugSeries.GetValueOrDefault(date));

        var readings = await db.MeterReadings.AsNoTracking()
            .Where(r => r.FlatId == flatId)
            .OrderBy(r => r.ReadingDate)
            .ToListAsync(ct);

        var mainMeterTotal = TryComputeMainMeterTotal(readings, startDate, endDate);

        decimal totalKwh;
        decimal totalCost;
        decimal residualKwh;
        decimal residualCost;

        if (mainMeterTotal is not null)
        {
            var mainMeterSeries = BuildMainMeterDailySeries(readings);
            totalKwh = mainMeterTotal.Value;
            totalCost = CostForDailySeries(date => mainMeterSeries.GetValueOrDefault(date));
            residualKwh = totalKwh - allDeviceKwh;
            residualCost = totalCost - allDeviceCost;
        }
        else
        {
            // No main-meter ground truth to reconcile against — fold in any orphaned plugged-but-
            // deviceless PowerPoint kWh here too, or it would vanish entirely (it can't reach
            // Residual, since Residual is forced to 0 in this branch).
            totalKwh = allDeviceKwh + orphanedPlugKwh;
            totalCost = allDeviceCost + orphanedPlugCost;
            residualKwh = 0m;
            residualCost = 0m;
        }

        return new DecompositionResponse(
            new PeriodRange(startDate, endDate),
            totalKwh, totalCost,
            IsUnavailable: false, hasInterpolatedData,
            new ResidualItem(residualKwh, residualCost),
            roomDecompositions);
    }

    private static DeviceDecomposition BuildSmartStripDecomposition(
        PowerPoint pp,
        Dictionary<string, Dictionary<DateOnly, decimal>> plugDailySeries,
        int dayCount,
        Func<Func<DateOnly, decimal>, decimal> costForDailySeries)
    {
        // plugDailySeries entries are already pre-filtered to [startDate, endDate] by the DB query,
        // so summing all values here is equivalent to summing over the period.
        var series = plugDailySeries.GetValueOrDefault(pp.PlugId!, []);
        var stripMeasuredTotal = series.Values.Sum();

        var estimates = pp.Devices.ToDictionary(d => d.DeviceId, d => EstimateDailyKwh(d) * dayCount);
        var configuredIds = pp.Devices
            .Where(d => d.ConsumptionApproach != ConsumptionApproach.None)
            .Select(d => d.DeviceId)
            .ToHashSet();
        var sumConfiguredEstimates = configuredIds.Sum(id => estimates[id]);

        var shares = new Dictionary<Guid, decimal>();
        if (sumConfiguredEstimates > 0m)
        {
            decimal sumConfiguredShares = 0m;
            foreach (var id in configuredIds)
            {
                var share = (estimates[id] / sumConfiguredEstimates) * stripMeasuredTotal;
                shares[id] = share;
                sumConfiguredShares += share;
            }

            var unconfiguredIds = pp.Devices
                .Where(d => !configuredIds.Contains(d.DeviceId))
                .Select(d => d.DeviceId)
                .ToList();
            if (unconfiguredIds.Count > 0)
            {
                var remainder = (stripMeasuredTotal - sumConfiguredShares) / unconfiguredIds.Count;
                foreach (var id in unconfiguredIds)
                    shares[id] = remainder;
            }
        }
        else
        {
            var equalShare = pp.Devices.Count > 0 ? stripMeasuredTotal / pp.Devices.Count : 0m;
            foreach (var d in pp.Devices)
                shares[d.DeviceId] = equalShare;
        }

        var subDevices = new List<SubDeviceDecomposition>();
        decimal subDeviceCostSum = 0m;
        foreach (var d in pp.Devices)
        {
            var ratio = stripMeasuredTotal != 0m
                ? shares[d.DeviceId] / stripMeasuredTotal
                : (pp.Devices.Count > 0 ? 1m / pp.Devices.Count : 0m);
            var subCost = costForDailySeries(date => ratio * series.GetValueOrDefault(date));
            subDeviceCostSum += subCost;
            var isConfigured = d.ConsumptionApproach != ConsumptionApproach.None;
            subDevices.Add(new SubDeviceDecomposition(
                d.DeviceId, d.Name, shares[d.DeviceId], subCost, isConfigured, !isConfigured));
        }

        return new DeviceDecomposition(
            pp.PowerPointId, pp.Name, stripMeasuredTotal, subDeviceCostSum,
            AttributionApproach.Measured, IsSmartStrip: true, subDevices);
    }

    private static decimal EstimateDailyKwh(Device device) => device.ConsumptionApproach switch
    {
        ConsumptionApproach.EuLabel => (device.EuAnnualKwh ?? 0m) / 365m,
        ConsumptionApproach.SelfMeasured => device.SelfMeasuredPeriod == SelfMeasuredPeriod.Weekly
            ? (device.SelfMeasuredKwh ?? 0m) / 7m
            : (device.SelfMeasuredKwh ?? 0m),
        _ => 0m
    };

    private static (AttributionApproach Approach, decimal DailyEstimate) ResolveStandaloneApproach(Device device) =>
        device.ConsumptionApproach switch
        {
            ConsumptionApproach.EuLabel => (AttributionApproach.EuLabel, EstimateDailyKwh(device)),
            ConsumptionApproach.SelfMeasured => (AttributionApproach.SelfMeasured, EstimateDailyKwh(device)),
            _ => (AttributionApproach.None, 0m)
        };

    private static DateTimeOffset ToLocalMidnight(DateOnly date) =>
        new(date.Year, date.Month, date.Day, 0, 0, 0, AppTimeZone.GetUtcOffset(date.ToDateTime(TimeOnly.MinValue)));

    // Duplicated verbatim from KpiCalculator.cs:155-164 per AC11/Task 5 — the DB-backed resolver
    // service has zero real callers today; this in-memory pattern is the codebase's only live precedent.
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

    // Duplicated verbatim from ReconciliationEngine.cs:64-82 per AC10/Task 2.
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

    // Duplicated verbatim from ReconciliationEngine.cs:84-103 per AC10/Task 2.
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
