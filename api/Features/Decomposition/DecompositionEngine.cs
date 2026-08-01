using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
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

        var assignmentPeriods = await db.DeviceAssignmentPeriods.AsNoTracking()
            .Where(p => p.FlatId == flatId)
            .ToListAsync(ct);
        var periodsByDeviceId = assignmentPeriods
            .GroupBy(p => p.DeviceId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DeviceAssignmentPeriod>)g.ToList());

        // Current structure snapshot used to resolve a resolved PowerPointId to its PlugId/RoomId
        // for any given day — PowerPoint/Room history isn't tracked, only Device->PowerPoint
        // history is (AD-8b), so "where is this PowerPoint today" is all that's available.
        var powerPointsById = rooms.SelectMany(r => r.PowerPoints).ToDictionary(pp => pp.PowerPointId);

        var dayCount = endDate.DayNumber - startDate.DayNumber + 1;

        decimal CostForDailySeries(Func<DateOnly, decimal> dailyKwh)
        {
            decimal cost = 0m;
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var tariff = TariffResolution.Resolve(tariffs, ToLocalMidnight(date));
                if (tariff is not null)
                    cost += dailyKwh(date) * tariff.PricePerKwh;
            }
            return cost;
        }

        // Keyed by RoomId, one empty list pre-seeded per room — a single Device's day-by-day
        // resolution can span more than one Room, so its DeviceDecomposition entries are merged in
        // here rather than appended to whichever room is currently being iterated (Task 5.4).
        var deviceDecompositionsByRoom = rooms.ToDictionary(r => r.RoomId, r => new List<DeviceDecomposition>());
        // A PowerPoint with a PlugId but zero attached Devices has no DeviceDecomposition to
        // attribute its measured kWh to (AC12, deferred to Story 7.3's UX). Track it separately so
        // the AC10 fallback branch below can still account for it in TotalKwh instead of losing it.
        var orphanedPlugSeries = new Dictionary<DateOnly, decimal>();
        // A day-by-day resolution can attribute a currently-zero-device PowerPoint's plug to some
        // other device's historical room (e.g. the device moved away from it) — the orphaned-plug
        // fallback below must skip those days, or they'd be counted both as that device's kWh and
        // again as orphaned kWh.
        var claimedPlugDays = new HashSet<(string PlugId, DateOnly Date)>();
        var orphanedCandidates = new List<PowerPoint>();

        foreach (var room in rooms)
        {
            foreach (var pp in room.PowerPoints)
            {
                if (pp.PlugId is not null && pp.Devices.Count == 1)
                {
                    var device = pp.Devices.Single();
                    AttributeDeviceDayByDay(
                        device, pp, periodsByDeviceId, powerPointsById, plugDailySeries,
                        startDate, endDate, CostForDailySeries, deviceDecompositionsByRoom, claimedPlugDays);
                }
                else if (pp.PlugId is not null && pp.Devices.Count > 1)
                {
                    deviceDecompositionsByRoom[room.RoomId].Add(
                        BuildSmartStripDecomposition(pp, plugDailySeries, dayCount, CostForDailySeries));
                }
                else if (pp.PlugId is not null && pp.Devices.Count == 0)
                {
                    orphanedCandidates.Add(pp);
                }
                else
                {
                    foreach (var device in pp.Devices)
                    {
                        AttributeDeviceDayByDay(
                            device, pp, periodsByDeviceId, powerPointsById, plugDailySeries,
                            startDate, endDate, CostForDailySeries, deviceDecompositionsByRoom, claimedPlugDays);
                    }
                }
            }
        }

        foreach (var pp in orphanedCandidates)
        {
            var series = plugDailySeries.GetValueOrDefault(pp.PlugId!, []);
            foreach (var (date, kwh) in series)
            {
                if (claimedPlugDays.Contains((pp.PlugId!, date))) continue;
                orphanedPlugSeries[date] = orphanedPlugSeries.GetValueOrDefault(date) + kwh;
            }
        }

        var roomDecompositions = rooms.Select(room =>
        {
            var deviceDecompositions = deviceDecompositionsByRoom[room.RoomId];
            var roomKwh = Round(deviceDecompositions.Sum(d => d.Kwh));
            var roomCost = Round(deviceDecompositions.Sum(d => d.Cost));
            return new RoomDecomposition(room.RoomId, room.Name, roomKwh, roomCost, deviceDecompositions);
        }).ToList();

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
            Round(totalKwh), Round(totalCost),
            IsUnavailable: false, hasInterpolatedData,
            new ResidualItem(Round(residualKwh), Round(residualCost)),
            roomDecompositions);
    }

    // Resolves the Room a Device's kWh/cost is attributed to on each day of the period, following
    // its DeviceAssignmentPeriod history rather than assuming its current, structurally-attached
    // PowerPoint applied for the whole period (Task 5). Handles both the measured (PlugId present)
    // and standalone-estimate (PlugId absent) cases, since a Device's resolved PowerPoint on a given
    // day may differ in kind from the PowerPoint it's currently attached to.
    private static void AttributeDeviceDayByDay(
        Device device,
        PowerPoint currentPowerPoint,
        IReadOnlyDictionary<Guid, IReadOnlyList<DeviceAssignmentPeriod>> periodsByDeviceId,
        IReadOnlyDictionary<Guid, PowerPoint> powerPointsById,
        Dictionary<string, Dictionary<DateOnly, decimal>> plugDailySeries,
        DateOnly startDate,
        DateOnly endDate,
        Func<Func<DateOnly, decimal>, decimal> costForDailySeries,
        Dictionary<Guid, List<DeviceDecomposition>> deviceDecompositionsByRoom,
        HashSet<(string PlugId, DateOnly Date)> claimedPlugDays)
    {
        var periods = periodsByDeviceId.GetValueOrDefault(device.DeviceId, []);
        var (standaloneApproach, dailyEstimate) = ResolveStandaloneApproach(device);

        var kwhByRoom = new Dictionary<Guid, Dictionary<DateOnly, decimal>>();
        var lastApproachByRoom = new Dictionary<Guid, AttributionApproach>();
        var lastPowerPointIdByRoom = new Dictionary<Guid, Guid>();

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var localDate = ToLocalMidnight(date);
            var resolvedPpId = DeviceAssignmentResolution.Resolve(periods, localDate);
            // A resolved historical PowerPoint that's now a Smart Power Strip (Devices.Count > 1) is
            // handled by the outer dispatch loop's own pool-math branch for its current sub-devices —
            // using it here too would double-count its plug kWh. A PowerPoint with zero current
            // Devices is the ordinary "device moved away" case (AC6) and must still resolve normally.
            var resolvedPp = resolvedPpId is not null
                && powerPointsById.TryGetValue(resolvedPpId.Value, out var found)
                && found.Devices.Count <= 1
                ? found
                : currentPowerPoint;

            decimal dayKwh;
            AttributionApproach dayApproach;
            if (resolvedPp.PlugId is not null)
            {
                dayKwh = plugDailySeries.GetValueOrDefault(resolvedPp.PlugId, []).GetValueOrDefault(date);
                dayApproach = AttributionApproach.Measured;
                claimedPlugDays.Add((resolvedPp.PlugId, date));
            }
            else
            {
                dayKwh = IsDeviceActiveOn(device, localDate) ? dailyEstimate : 0m;
                dayApproach = standaloneApproach;
            }

            if (!kwhByRoom.TryGetValue(resolvedPp.RoomId, out var roomSeries))
            {
                roomSeries = new Dictionary<DateOnly, decimal>();
                kwhByRoom[resolvedPp.RoomId] = roomSeries;
            }
            roomSeries[date] = dayKwh;
            lastApproachByRoom[resolvedPp.RoomId] = dayApproach;
            lastPowerPointIdByRoom[resolvedPp.RoomId] = resolvedPp.PowerPointId;
        }

        foreach (var (roomId, roomSeries) in kwhByRoom)
        {
            var kwh = roomSeries.Values.Sum();
            var cost = costForDailySeries(d => roomSeries.GetValueOrDefault(d));
            deviceDecompositionsByRoom[roomId].Add(new DeviceDecomposition(
                device.DeviceId, lastPowerPointIdByRoom[roomId], device.Name,
                Round(kwh), Round(cost), lastApproachByRoom[roomId], IsSmartStrip: false, SubDevices: null));
        }
    }

    private static decimal Round(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

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
        var unconfiguredIds = pp.Devices
            .Where(d => !configuredIds.Contains(d.DeviceId))
            .Select(d => d.DeviceId)
            .ToList();
        var sumConfiguredEstimates = configuredIds.Sum(id => estimates[id]);
        var nominalWeight = configuredIds.Count > 0 ? sumConfiguredEstimates / configuredIds.Count : 0m;
        var poolTotal = sumConfiguredEstimates + (unconfiguredIds.Count * nominalWeight);

        var shares = new Dictionary<Guid, decimal>();
        if (poolTotal > 0m)
        {
            foreach (var id in configuredIds)
                shares[id] = (estimates[id] / poolTotal) * stripMeasuredTotal;
            foreach (var id in unconfiguredIds)
                shares[id] = (nominalWeight / poolTotal) * stripMeasuredTotal;
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
                d.DeviceId, d.Name, Round(shares[d.DeviceId]), Round(subCost), isConfigured, !isConfigured));
        }

        return new DeviceDecomposition(
            pp.PowerPointId, pp.PowerPointId, pp.Name, Round(stripMeasuredTotal), Round(subDeviceCostSum),
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

    private static bool IsDeviceActiveOn(Device device, DateTimeOffset date) =>
        (device.InUseSince is null || device.InUseSince <= date) &&
        (device.DecommissionedDate is null || date <= device.DecommissionedDate);

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
