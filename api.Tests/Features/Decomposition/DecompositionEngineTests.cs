using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Decomposition;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api.Tests.Features.Decomposition;

public class DecompositionEngineTests
{
    private static AppDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static DecompositionEngine MakeEngine(AppDbContext db) => new(db);

    // Noon UTC keeps the local (Europe/Berlin) calendar date unambiguous regardless of DST.
    private static DateTimeOffset NoonUtc(int year, int month, int day) => new(year, month, day, 12, 0, 0, TimeSpan.Zero);

    // Matches DecompositionEngine's own ToLocalMidnight construction exactly (Europe/Berlin is
    // UTC+1 with no DST in January), so InUseSince/DecommissionedDate boundary tests below compare
    // against the identical instant the engine itself compares against.
    private static DateTimeOffset LocalMidnight(int year, int month, int day) => new(year, month, day, 0, 0, 0, TimeSpan.FromHours(1));

    private static async Task<Room> SeedRoomAsync(AppDbContext db, Guid flatId, string name = "Room")
    {
        var room = new Room { RoomId = Guid.NewGuid(), FlatId = flatId, Name = name };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return room;
    }

    private static async Task<PowerPoint> SeedPowerPointAsync(AppDbContext db, Guid roomId, string name, string? plugId = null)
    {
        var pp = new PowerPoint { PowerPointId = Guid.NewGuid(), RoomId = roomId, Name = name, PlugId = plugId };
        db.PowerPoints.Add(pp);
        await db.SaveChangesAsync();
        return pp;
    }

    private static async Task<Device> SeedDeviceAsync(
        AppDbContext db, Guid powerPointId, string name,
        ConsumptionApproach approach = ConsumptionApproach.None,
        decimal? euAnnualKwh = null,
        decimal? selfMeasuredKwh = null,
        SelfMeasuredPeriod? selfMeasuredPeriod = null,
        DateTimeOffset? inUseSince = null,
        DateTimeOffset? decommissionedDate = null)
    {
        var device = new Device
        {
            DeviceId = Guid.NewGuid(),
            PowerPointId = powerPointId,
            Name = name,
            ConsumptionApproach = approach,
            EuAnnualKwh = euAnnualKwh,
            SelfMeasuredKwh = selfMeasuredKwh,
            SelfMeasuredPeriod = selfMeasuredPeriod,
            InUseSince = inUseSince,
            DecommissionedDate = decommissionedDate
        };
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        return device;
    }

    private static async Task SeedDailyRowAsync(AppDbContext db, Guid flatId, string plugId, DateOnly date, decimal kwh, bool isInterpolated = false)
    {
        db.SmartPlugDailyData.Add(new SmartPlugDailyData
        {
            FlatId = flatId,
            PlugId = plugId,
            Date = date,
            KwhValue = kwh,
            IsInterpolated = isInterpolated
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedReadingAsync(AppDbContext db, Guid flatId, DateTimeOffset date, decimal kwh)
    {
        db.MeterReadings.Add(new MeterReading { FlatId = flatId, ReadingDate = date, KwhValue = kwh });
        await db.SaveChangesAsync();
    }

    private static async Task SeedTariffAsync(AppDbContext db, Guid flatId, DateTimeOffset contractStart, decimal pricePerKwh)
    {
        db.Tariffs.Add(new Tariff { FlatId = flatId, ContractStartDate = contractStart, PricePerKwh = pricePerKwh, MonthlyBaseFee = 0m });
        await db.SaveChangesAsync();
    }

    private static async Task SeedAssignmentPeriodAsync(
        AppDbContext db, Guid deviceId, Guid powerPointId, Guid flatId, DateTimeOffset from, DateTimeOffset? to = null)
    {
        db.DeviceAssignmentPeriods.Add(new DeviceAssignmentPeriod
        {
            DeviceId = deviceId,
            PowerPointId = powerPointId,
            FlatId = flatId,
            From = from,
            To = to
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ComputeAsync_MeasuredSingleDevicePowerPoint_SumsPlugKwhAndAppliesTariff()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId, "Living Room");
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket", plugId: "plug-1");
        await SeedDeviceAsync(db, pp.PowerPointId, "TV", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 1), 2m);
        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 2), 3m);
        await SeedTariffAsync(db, flatId, NoonUtc(2025, 1, 1), pricePerKwh: 0.30m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), CancellationToken.None);

        var device = result.Rooms.Single().Devices.Single();
        device.Kwh.ShouldBe(5m);
        device.Approach.ShouldBe(AttributionApproach.Measured);
        device.IsSmartStrip.ShouldBeFalse();
        device.Cost.ShouldBe(1.5m);
        device.PowerPointId.ShouldBe(pp.PowerPointId);
    }

    [Fact]
    public async Task ComputeAsync_DeviceWithOnlyItsBackfilledPeriod_MatchesZeroPeriodBaseline()
    {
        // Literal AC2 backfill shape: a single open period whose PowerPointId already matches the
        // device's current PowerPointId. This exercises DeviceAssignmentResolution's resolved,
        // non-null branch — distinct from the null-fallback branch a zero-period device takes — and
        // must produce identical output.
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId, "Living Room");
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket", plugId: "plug-1");
        var device = await SeedDeviceAsync(db, pp.PowerPointId, "TV", approach: ConsumptionApproach.None);
        await SeedAssignmentPeriodAsync(db, device.DeviceId, pp.PowerPointId, flatId, DateTimeOffset.MinValue, null);
        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 1), 2m);
        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 2), 3m);
        await SeedTariffAsync(db, flatId, NoonUtc(2025, 1, 1), pricePerKwh: 0.30m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), CancellationToken.None);

        var deviceResult = result.Rooms.Single().Devices.Single();
        deviceResult.Kwh.ShouldBe(5m);
        deviceResult.Approach.ShouldBe(AttributionApproach.Measured);
        deviceResult.Cost.ShouldBe(1.5m);
        deviceResult.PowerPointId.ShouldBe(pp.PowerPointId);
    }

    [Fact]
    public async Task ComputeAsync_StandaloneNoneApproachDevice_ExposesContainingPowerPointId()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId, "Living Room");
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Unplugged Socket");
        await SeedDeviceAsync(db, pp.PowerPointId, "Old Lamp", approach: ConsumptionApproach.None);
        // Unrelated plug elsewhere, just to make SmartPlugDailyData non-empty for the flat.
        var otherPp = await SeedPowerPointAsync(db, room.RoomId, "Other Socket", plugId: "plug-other");
        await SeedDeviceAsync(db, otherPp.PowerPointId, "Other", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-other", new DateOnly(2026, 1, 1), 1m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), CancellationToken.None);

        var device = result.Rooms.Single().Devices.Single(d => d.Name == "Old Lamp");
        device.PowerPointId.ShouldBe(pp.PowerPointId);
        device.PowerPointId.ShouldNotBe(device.DeviceId);
    }

    [Fact]
    public async Task ComputeAsync_EuLabelDevice_ProjectsDailyEstimateAcrossPeriod()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Unplugged Socket");
        await SeedDeviceAsync(db, pp.PowerPointId, "Fridge", approach: ConsumptionApproach.EuLabel, euAnnualKwh: 365m);
        // Unrelated plug elsewhere, just to make SmartPlugDailyData non-empty for the flat.
        var otherPp = await SeedPowerPointAsync(db, room.RoomId, "Other Socket", plugId: "plug-other");
        await SeedDeviceAsync(db, otherPp.PowerPointId, "Other", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-other", new DateOnly(2026, 1, 1), 1m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5), CancellationToken.None);

        var device = result.Rooms.Single().Devices.Single(d => d.Name == "Fridge");
        device.Kwh.ShouldBe(5m);
        device.Approach.ShouldBe(AttributionApproach.EuLabel);
    }

    [Fact]
    public async Task ComputeAsync_SelfMeasuredDailyPeriod_UsesKwhValueDirectly()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Unplugged Socket");
        await SeedDeviceAsync(db, pp.PowerPointId, "Lamp", approach: ConsumptionApproach.SelfMeasured, selfMeasuredKwh: 2m, selfMeasuredPeriod: SelfMeasuredPeriod.Daily);
        var otherPp = await SeedPowerPointAsync(db, room.RoomId, "Other Socket", plugId: "plug-other");
        await SeedDeviceAsync(db, otherPp.PowerPointId, "Other", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-other", new DateOnly(2026, 1, 1), 1m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), CancellationToken.None);

        var device = result.Rooms.Single().Devices.Single(d => d.Name == "Lamp");
        device.Kwh.ShouldBe(6m);
        device.Approach.ShouldBe(AttributionApproach.SelfMeasured);
    }

    [Fact]
    public async Task ComputeAsync_SelfMeasuredWeeklyPeriod_DividesBySeven()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Unplugged Socket");
        await SeedDeviceAsync(db, pp.PowerPointId, "Lamp", approach: ConsumptionApproach.SelfMeasured, selfMeasuredKwh: 7m, selfMeasuredPeriod: SelfMeasuredPeriod.Weekly);
        var otherPp = await SeedPowerPointAsync(db, room.RoomId, "Other Socket", plugId: "plug-other");
        await SeedDeviceAsync(db, otherPp.PowerPointId, "Other", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-other", new DateOnly(2026, 1, 1), 1m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), CancellationToken.None);

        var device = result.Rooms.Single().Devices.Single(d => d.Name == "Lamp");
        device.Kwh.ShouldBe(3m); // 7/7 = 1 per day * 3 days
        device.Approach.ShouldBe(AttributionApproach.SelfMeasured);
    }

    [Fact]
    public async Task ComputeAsync_NoPlugAndNoApproach_ContributesZeroKwh()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Unplugged Socket");
        await SeedDeviceAsync(db, pp.PowerPointId, "Unconfigured Device", approach: ConsumptionApproach.None);
        var otherPp = await SeedPowerPointAsync(db, room.RoomId, "Other Socket", plugId: "plug-other");
        await SeedDeviceAsync(db, otherPp.PowerPointId, "Other", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-other", new DateOnly(2026, 1, 1), 1m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), CancellationToken.None);

        var device = result.Rooms.Single().Devices.Single(d => d.Name == "Unconfigured Device");
        device.Kwh.ShouldBe(0m);
        device.Approach.ShouldBe(AttributionApproach.None);
    }

    [Fact]
    public async Task ComputeAsync_SmartPowerStripMixed_UnconfiguredDevicesGetBlendedNominalShare()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Strip", plugId: "strip-1");
        await SeedDeviceAsync(db, pp.PowerPointId, "DeviceA", approach: ConsumptionApproach.EuLabel, euAnnualKwh: 730m); // daily = 2
        await SeedDeviceAsync(db, pp.PowerPointId, "DeviceB", approach: ConsumptionApproach.SelfMeasured, selfMeasuredKwh: 6m, selfMeasuredPeriod: SelfMeasuredPeriod.Daily); // daily = 6
        await SeedDeviceAsync(db, pp.PowerPointId, "DeviceC", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "strip-1", new DateOnly(2026, 1, 1), 80m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), CancellationToken.None);

        var strip = result.Rooms.Single().Devices.Single();
        strip.IsSmartStrip.ShouldBeTrue();
        strip.Kwh.ShouldBe(80m);
        strip.SubDevices.ShouldNotBeNull();
        var a = strip.SubDevices!.Single(d => d.Name == "DeviceA");
        var b = strip.SubDevices!.Single(d => d.Name == "DeviceB");
        var c = strip.SubDevices!.Single(d => d.Name == "DeviceC");
        // sumConfiguredEstimates=8, nominalWeight=8/2=4, poolTotal=8+1*4=12
        a.Kwh.ShouldBe(80m * 2m / 12m, tolerance: 0.01m); // 13.333...
        a.IsConfigured.ShouldBeTrue();
        b.Kwh.ShouldBe(80m * 6m / 12m, tolerance: 0.01m); // 40
        b.IsConfigured.ShouldBeTrue();
        c.Kwh.ShouldBe(80m * 4m / 12m, tolerance: 0.01m); // 26.666... — the blended nominal share, must be real and non-zero
        c.IsUnconfigured.ShouldBeTrue();
        (a.Kwh + b.Kwh + c.Kwh).ShouldBe(strip.Kwh, tolerance: 0.01m);
    }

    [Fact]
    public async Task ComputeAsync_SmartPowerStripFullyConfigured_SplitsProportionallyUnchanged()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Strip", plugId: "strip-3");
        await SeedDeviceAsync(db, pp.PowerPointId, "DeviceA", approach: ConsumptionApproach.EuLabel, euAnnualKwh: 730m); // daily = 2
        await SeedDeviceAsync(db, pp.PowerPointId, "DeviceB", approach: ConsumptionApproach.SelfMeasured, selfMeasuredKwh: 6m, selfMeasuredPeriod: SelfMeasuredPeriod.Daily); // daily = 6
        await SeedDailyRowAsync(db, flatId, "strip-3", new DateOnly(2026, 1, 1), 80m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), CancellationToken.None);

        var strip = result.Rooms.Single().Devices.Single();
        strip.SubDevices.ShouldNotBeNull();
        var a = strip.SubDevices!.Single(d => d.Name == "DeviceA");
        var b = strip.SubDevices!.Single(d => d.Name == "DeviceB");
        a.Kwh.ShouldBe(20m); // 2/8 * 80 — unchanged from the pre-fix formula since poolTotal == sumConfiguredEstimates here
        a.IsConfigured.ShouldBeTrue();
        b.Kwh.ShouldBe(60m); // 6/8 * 80
        b.IsConfigured.ShouldBeTrue();
        (a.Kwh + b.Kwh).ShouldBe(strip.Kwh, tolerance: 0.01m);
        strip.PowerPointId.ShouldBe(pp.PowerPointId);
        strip.DeviceId.ShouldBe(pp.PowerPointId);
    }

    [Fact]
    public async Task ComputeAsync_SmartPowerStripAllUnconfigured_SplitsEquallyWithoutDivideByZero()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Strip", plugId: "strip-2");
        await SeedDeviceAsync(db, pp.PowerPointId, "DeviceA", approach: ConsumptionApproach.None);
        await SeedDeviceAsync(db, pp.PowerPointId, "DeviceB", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "strip-2", new DateOnly(2026, 1, 1), 50m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), CancellationToken.None);

        var strip = result.Rooms.Single().Devices.Single();
        strip.SubDevices.ShouldNotBeNull();
        strip.SubDevices!.Count.ShouldBe(2);
        strip.SubDevices!.ShouldAllBe(d => d.Kwh == 25m);
        strip.SubDevices!.ShouldAllBe(d => d.IsUnconfigured);
    }

    [Fact]
    public async Task ComputeAsync_SmartPowerStripConfiguredEstimatesSumToZero_FallsBackToEqualSplit()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Strip", plugId: "strip-4");
        await SeedDeviceAsync(db, pp.PowerPointId, "DeviceA", approach: ConsumptionApproach.EuLabel, euAnnualKwh: null); // configured but estimate = 0
        await SeedDeviceAsync(db, pp.PowerPointId, "DeviceB", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "strip-4", new DateOnly(2026, 1, 1), 40m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), CancellationToken.None);

        var strip = result.Rooms.Single().Devices.Single();
        strip.SubDevices.ShouldNotBeNull();
        // sumConfiguredEstimates=0 -> nominalWeight=0 -> poolTotal=0 -> equal-split else branch, same as zero-configured-devices case
        strip.SubDevices!.ShouldAllBe(d => d.Kwh == 20m);
        var a = strip.SubDevices!.Single(d => d.Name == "DeviceA");
        a.IsConfigured.ShouldBeTrue();
    }

    [Fact]
    public async Task ComputeAsync_SmartPowerStripSingleConfiguredDevice_UnconfiguredGetItsEstimateAsNominalWeight()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Strip", plugId: "strip-5");
        await SeedDeviceAsync(db, pp.PowerPointId, "DeviceA", approach: ConsumptionApproach.EuLabel, euAnnualKwh: 730m); // daily = 2
        await SeedDeviceAsync(db, pp.PowerPointId, "DeviceB", approach: ConsumptionApproach.None);
        await SeedDeviceAsync(db, pp.PowerPointId, "DeviceC", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "strip-5", new DateOnly(2026, 1, 1), 30m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), CancellationToken.None);

        var strip = result.Rooms.Single().Devices.Single();
        strip.SubDevices.ShouldNotBeNull();
        // configuredIds.Count=1 -> nominalWeight = sumConfiguredEstimates/1 = the sole device's own estimate
        // poolTotal = 2 + 2*2 = 6 -> every device (configured and unconfigured alike) gets an equal 30*2/6 = 10 share
        // (2/6 is a repeating decimal, so use tolerance rather than exact equality)
        strip.SubDevices!.ShouldAllBe(d => Math.Abs(d.Kwh - 10m) < 0.01m);
        var a = strip.SubDevices!.Single(d => d.Name == "DeviceA");
        a.IsConfigured.ShouldBeTrue();
        strip.SubDevices!.Where(d => d.Name != "DeviceA").ShouldAllBe(d => d.IsUnconfigured);
    }

    [Fact]
    public async Task ComputeAsync_SmartPowerStripMultipleUnconfiguredDevices_EachGetsIdenticalNonZeroShare()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Strip", plugId: "strip-6");
        await SeedDeviceAsync(db, pp.PowerPointId, "DeviceA", approach: ConsumptionApproach.EuLabel, euAnnualKwh: 730m); // daily = 2
        await SeedDeviceAsync(db, pp.PowerPointId, "DeviceB", approach: ConsumptionApproach.SelfMeasured, selfMeasuredKwh: 6m, selfMeasuredPeriod: SelfMeasuredPeriod.Daily); // daily = 6
        await SeedDeviceAsync(db, pp.PowerPointId, "DeviceC", approach: ConsumptionApproach.None);
        await SeedDeviceAsync(db, pp.PowerPointId, "DeviceD", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "strip-6", new DateOnly(2026, 1, 1), 80m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), CancellationToken.None);

        var strip = result.Rooms.Single().Devices.Single();
        strip.SubDevices.ShouldNotBeNull();
        // sumConfiguredEstimates=8, nominalWeight=8/2=4, poolTotal=8+2*4=16
        var a = strip.SubDevices!.Single(d => d.Name == "DeviceA");
        var b = strip.SubDevices!.Single(d => d.Name == "DeviceB");
        var c = strip.SubDevices!.Single(d => d.Name == "DeviceC");
        var d = strip.SubDevices!.Single(d => d.Name == "DeviceD");
        a.Kwh.ShouldBe(10m); // 2/16 * 80
        b.Kwh.ShouldBe(30m); // 6/16 * 80
        c.Kwh.ShouldBe(20m); // 4/16 * 80
        d.Kwh.ShouldBe(20m); // 4/16 * 80 — identical to c, both unconfigured
        c.IsUnconfigured.ShouldBeTrue();
        d.IsUnconfigured.ShouldBeTrue();
        (a.Kwh + b.Kwh + c.Kwh + d.Kwh).ShouldBe(strip.Kwh, tolerance: 0.01m);
    }

    [Fact]
    public async Task ComputeAsync_CleanPeriod_ResidualWithinTightTolerance()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket", plugId: "plug-1");
        await SeedDeviceAsync(db, pp.PowerPointId, "TV", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 5), 100m);
        await SeedReadingAsync(db, flatId, NoonUtc(2026, 1, 1), 0m);
        await SeedReadingAsync(db, flatId, NoonUtc(2026, 1, 11), 100m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 11), CancellationToken.None);

        result.HasInterpolatedData.ShouldBeFalse();
        result.TotalKwh.ShouldBe(100m);
        Math.Abs(result.Residual.Kwh).ShouldBeLessThanOrEqualTo(0.1m);
        result.Residual.ShouldNotBeNull();
    }

    [Fact]
    public async Task ComputeAsync_InterpolatedPeriod_ResidualWithinWiderTolerance()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket", plugId: "plug-1");
        await SeedDeviceAsync(db, pp.PowerPointId, "TV", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 5), 90m);
        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 6), 9.1m, isInterpolated: true);
        await SeedReadingAsync(db, flatId, NoonUtc(2026, 1, 1), 0m);
        await SeedReadingAsync(db, flatId, NoonUtc(2026, 1, 11), 100m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 11), CancellationToken.None);

        result.HasInterpolatedData.ShouldBeTrue();
        Math.Abs(result.Residual.Kwh).ShouldBeLessThanOrEqualTo(1.0m);
    }

    [Fact]
    public async Task ComputeAsync_NoSmartPlugDailyData_ReturnsUnavailableWithZeroedFigures()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket", plugId: "plug-1");
        await SeedDeviceAsync(db, pp.PowerPointId, "TV", approach: ConsumptionApproach.None);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5), CancellationToken.None);

        result.IsUnavailable.ShouldBeTrue();
        result.Rooms.ShouldBeEmpty();
        result.TotalKwh.ShouldBe(0m);
        result.TotalCost.ShouldBe(0m);
        result.Residual.Kwh.ShouldBe(0m);
        result.Residual.Cost.ShouldBe(0m);
    }

    [Fact]
    public async Task ComputeAsync_AnyInterpolatedRow_SetsHasInterpolatedDataTrue()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket", plugId: "plug-1");
        await SeedDeviceAsync(db, pp.PowerPointId, "TV", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 1), 1m);
        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 2), 2m, isInterpolated: true);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), CancellationToken.None);

        result.HasInterpolatedData.ShouldBeTrue();
    }

    [Fact]
    public async Task ComputeAsync_InsufficientCoverageFallback_AllKwhAttributed_ResidualKwhIsZeroButPresent()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket", plugId: "plug-1");
        await SeedDeviceAsync(db, pp.PowerPointId, "TV", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 5), 100m);
        // Only one reading -> insufficient coverage -> fallback: TotalKwh = attributed, Residual.Kwh = 0.
        await SeedReadingAsync(db, flatId, NoonUtc(2026, 1, 1), 0m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 11), CancellationToken.None);

        result.Residual.ShouldNotBeNull();
        result.Residual.Kwh.ShouldBe(0m);
    }

    [Fact]
    public async Task ComputeAsync_RealReconciliationFullyAccountsForAttributedKwh_ResidualKwhIsExactlyZero()
    {
        // Distinct from the insufficient-coverage fallback test above: this scenario has full
        // MeterReading coverage (mainMeterTotal is not null), so TotalKwh comes from the real
        // day-allocation reconciliation, not the AC10 fallback — and it still lands at an exact
        // zero residual because 100% of the reconciled main-meter kWh is attributed to the one device.
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket", plugId: "plug-1");
        await SeedDeviceAsync(db, pp.PowerPointId, "TV", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 5), 100m);
        await SeedReadingAsync(db, flatId, NoonUtc(2026, 1, 1), 0m);
        await SeedReadingAsync(db, flatId, NoonUtc(2026, 1, 11), 100m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 11), CancellationToken.None);

        result.TotalKwh.ShouldBe(100m);
        result.Residual.ShouldNotBeNull();
        result.Residual.Kwh.ShouldBe(0m);
    }

    [Fact]
    public async Task TotalKwh_MultipleReadingsOnSameLocalCalendarDay_TelescopesCorrectlyWithoutDoubleCounting()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket", plugId: "plug-1");
        await SeedDeviceAsync(db, pp.PowerPointId, "TV", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 5), 1m);

        var first = NoonUtc(2026, 1, 1);
        await SeedReadingAsync(db, flatId, first, 0m);
        // Two readings landing on the same local calendar day (Jan 5), monotonically increasing.
        await SeedReadingAsync(db, flatId, new DateTimeOffset(2026, 1, 5, 8, 0, 0, TimeSpan.Zero), 50m);
        await SeedReadingAsync(db, flatId, new DateTimeOffset(2026, 1, 5, 20, 0, 0, TimeSpan.Zero), 70m);
        var last = NoonUtc(2026, 1, 10);
        await SeedReadingAsync(db, flatId, last, 100m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 10), CancellationToken.None);

        result.TotalKwh.ShouldBe(100m); // last.KwhValue - first.KwhValue, telescoping across all 4 readings
    }

    [Fact]
    public async Task TotalKwh_PartialRangeStartingOnSameLocalDayDuplicateReadings_DoesNotInflateResult()
    {
        // The full-range telescoping test above can never fail regardless of how a same-local-day
        // pair of readings is internally day-allocated, because summing all days back to the very
        // first/last reading is always exactly last.KwhValue - first.KwhValue by construction. To
        // actually exercise (and prove correct, not just assert) the day-allocation logic around the
        // duplicate-day boundary, this test queries a PARTIAL range starting exactly on the duplicate
        // day (Jan 5), so a genuine over/under-allocation bug there would show up as a wrong total.
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket", plugId: "plug-1");
        await SeedDeviceAsync(db, pp.PowerPointId, "TV", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 5), 1m);

        await SeedReadingAsync(db, flatId, NoonUtc(2026, 1, 1), 0m);
        // Two readings landing on the same local calendar day (Jan 5), monotonically increasing.
        await SeedReadingAsync(db, flatId, new DateTimeOffset(2026, 1, 5, 8, 0, 0, TimeSpan.Zero), 50m);
        await SeedReadingAsync(db, flatId, new DateTimeOffset(2026, 1, 5, 20, 0, 0, TimeSpan.Zero), 70m);
        await SeedReadingAsync(db, flatId, NoonUtc(2026, 1, 10), 100m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 10), CancellationToken.None);

        // Hand-derived day-allocation: Jan2-4 each get (50-0)/4 = 12.5 (interval Jan1->Jan5 08:00).
        // Jan5 gets that same interval's endpoint share (12.5) PLUS the full same-day delta from the
        // 08:00->20:00 interval (20), i.e. 32.5 — not double-counted, since these are two genuinely
        // different, non-overlapping physical intervals that both happen to land on Jan 5's calendar
        // day. Jan6-10 each get (100-70)/5 = 6. Sum for the queried range [Jan5, Jan10] = 32.5 + 5*6.
        result.TotalKwh.ShouldBe(62.5m);
    }

    [Fact]
    public async Task TotalKwh_InsufficientMeterReadingCoverage_FallsBackToZeroResidualNotFalseNonZero()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket", plugId: "plug-1");
        await SeedDeviceAsync(db, pp.PowerPointId, "TV", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 5), 42m);
        // Zero MeterReadings at all -> guard returns null.

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 11), CancellationToken.None);

        result.IsUnavailable.ShouldBeFalse();
        result.TotalKwh.ShouldBe(42m); // falls back to sum of attributed device kWh
        result.Residual.Kwh.ShouldBe(0m);
        result.Residual.Cost.ShouldBe(0m);
    }

    [Fact]
    public async Task ComputeAsync_PluggedPowerPointWithZeroDevices_ContributesToTotalKwhInFallbackBranch()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        // Plug wired up with no Device row registered yet — no DeviceDecomposition entry is possible
        // for it (AC12), but its measured kWh must still be accounted for somewhere.
        await SeedPowerPointAsync(db, room.RoomId, "Orphaned Socket", plugId: "plug-orphan");
        await SeedDailyRowAsync(db, flatId, "plug-orphan", new DateOnly(2026, 1, 5), 42m);
        // Zero MeterReadings -> insufficient coverage -> AC10 fallback branch.

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 11), CancellationToken.None);

        result.IsUnavailable.ShouldBeFalse();
        result.Rooms.Single().Devices.ShouldBeEmpty();
        result.TotalKwh.ShouldBe(42m);
        result.Residual.Kwh.ShouldBe(0m);
    }

    [Fact]
    public async Task ComputeAsync_ApproachIsComputedNotCastFromStorageEnum()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);

        // Same-shaped device (both EuLabel-configured), but one sits behind a solo smart plug.
        var measuredPp = await SeedPowerPointAsync(db, room.RoomId, "Plugged Socket", plugId: "plug-1");
        await SeedDeviceAsync(db, measuredPp.PowerPointId, "MeasuredDevice", approach: ConsumptionApproach.EuLabel, euAnnualKwh: 365m);
        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 1), 3m);

        var unpluggedPp = await SeedPowerPointAsync(db, room.RoomId, "Unplugged Socket");
        await SeedDeviceAsync(db, unpluggedPp.PowerPointId, "EuLabelDevice", approach: ConsumptionApproach.EuLabel, euAnnualKwh: 365m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), CancellationToken.None);

        var measured = result.Rooms.Single().Devices.Single(d => d.Name == "MeasuredDevice");
        var euLabel = result.Rooms.Single().Devices.Single(d => d.Name == "EuLabelDevice");
        measured.Approach.ShouldBe(AttributionApproach.Measured);
        euLabel.Approach.ShouldBe(AttributionApproach.EuLabel);
    }

    [Fact]
    public async Task ComputeAsync_InUseSinceMidPeriod_CountsOnlyFromThatDateForward()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Unplugged Socket");
        await SeedDeviceAsync(db, pp.PowerPointId, "Fridge", approach: ConsumptionApproach.EuLabel, euAnnualKwh: 365m,
            inUseSince: LocalMidnight(2026, 1, 3));
        var otherPp = await SeedPowerPointAsync(db, room.RoomId, "Other Socket", plugId: "plug-other");
        await SeedDeviceAsync(db, otherPp.PowerPointId, "Other", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-other", new DateOnly(2026, 1, 1), 1m);
        await SeedTariffAsync(db, flatId, NoonUtc(2025, 1, 1), pricePerKwh: 0.30m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5), CancellationToken.None);

        var device = result.Rooms.Single().Devices.Single(d => d.Name == "Fridge");
        device.Kwh.ShouldBe(3m); // active Jan 3, 4, 5 -> 3 days * 1 kWh/day
        device.Cost.ShouldBe(0.9m);
    }

    [Fact]
    public async Task ComputeAsync_DecommissionedDateMidPeriod_StopsCountingAfterThatDate()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Unplugged Socket");
        await SeedDeviceAsync(db, pp.PowerPointId, "Fridge", approach: ConsumptionApproach.EuLabel, euAnnualKwh: 365m,
            decommissionedDate: LocalMidnight(2026, 1, 3));
        var otherPp = await SeedPowerPointAsync(db, room.RoomId, "Other Socket", plugId: "plug-other");
        await SeedDeviceAsync(db, otherPp.PowerPointId, "Other", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-other", new DateOnly(2026, 1, 1), 1m);
        await SeedTariffAsync(db, flatId, NoonUtc(2025, 1, 1), pricePerKwh: 0.30m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5), CancellationToken.None);

        var device = result.Rooms.Single().Devices.Single(d => d.Name == "Fridge");
        device.Kwh.ShouldBe(3m); // active Jan 1, 2, 3 -> 3 days * 1 kWh/day
        device.Cost.ShouldBe(0.9m);
    }

    [Fact]
    public async Task ComputeAsync_BothDatesSetWithActiveWindowFullyInsideQueryPeriod_CountsOnlyActiveDays()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Unplugged Socket");
        await SeedDeviceAsync(db, pp.PowerPointId, "Fridge", approach: ConsumptionApproach.EuLabel, euAnnualKwh: 365m,
            inUseSince: LocalMidnight(2026, 1, 3), decommissionedDate: LocalMidnight(2026, 1, 6));
        var otherPp = await SeedPowerPointAsync(db, room.RoomId, "Other Socket", plugId: "plug-other");
        await SeedDeviceAsync(db, otherPp.PowerPointId, "Other", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-other", new DateOnly(2026, 1, 1), 1m);
        await SeedTariffAsync(db, flatId, NoonUtc(2025, 1, 1), pricePerKwh: 0.30m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10), CancellationToken.None);

        var device = result.Rooms.Single().Devices.Single(d => d.Name == "Fridge");
        device.Kwh.ShouldBe(4m); // active Jan 3-6 inclusive -> 4 days * 1 kWh/day
        device.Cost.ShouldBe(1.2m);
    }

    [Fact]
    public async Task ComputeAsync_ExistenceWindowEntirelyOutsideQueryPeriod_ContributesZeroWithoutException()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var ppBefore = await SeedPowerPointAsync(db, room.RoomId, "Before Socket");
        await SeedDeviceAsync(db, ppBefore.PowerPointId, "RetiredBeforePeriod", approach: ConsumptionApproach.EuLabel, euAnnualKwh: 365m,
            inUseSince: LocalMidnight(2026, 1, 1), decommissionedDate: LocalMidnight(2026, 1, 5));
        var ppAfter = await SeedPowerPointAsync(db, room.RoomId, "After Socket");
        await SeedDeviceAsync(db, ppAfter.PowerPointId, "NotYetInstalledAfterPeriod", approach: ConsumptionApproach.EuLabel, euAnnualKwh: 365m,
            inUseSince: LocalMidnight(2026, 2, 1));
        var otherPp = await SeedPowerPointAsync(db, room.RoomId, "Other Socket", plugId: "plug-other");
        await SeedDeviceAsync(db, otherPp.PowerPointId, "Other", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-other", new DateOnly(2026, 1, 10), 1m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 15), CancellationToken.None);

        var before = result.Rooms.Single().Devices.Single(d => d.Name == "RetiredBeforePeriod");
        var after = result.Rooms.Single().Devices.Single(d => d.Name == "NotYetInstalledAfterPeriod");
        before.Kwh.ShouldBe(0m);
        before.Cost.ShouldBe(0m);
        after.Kwh.ShouldBe(0m);
        after.Cost.ShouldBe(0m);
    }

    [Fact]
    public async Task ComputeAsync_NeitherExistenceDateSet_FullPeriodInclusionUnchanged()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Unplugged Socket");
        await SeedDeviceAsync(db, pp.PowerPointId, "Fridge", approach: ConsumptionApproach.EuLabel, euAnnualKwh: 365m);
        var otherPp = await SeedPowerPointAsync(db, room.RoomId, "Other Socket", plugId: "plug-other");
        await SeedDeviceAsync(db, otherPp.PowerPointId, "Other", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-other", new DateOnly(2026, 1, 1), 1m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5), CancellationToken.None);

        var device = result.Rooms.Single().Devices.Single(d => d.Name == "Fridge");
        device.Kwh.ShouldBe(5m); // full 5-day period, unchanged from pre-story behavior
    }

    [Fact]
    public async Task ComputeAsync_SmartPowerStripSubDeviceWithExistenceWindow_PoolMathUnaffected()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Strip", plugId: "strip-existence");
        // DeviceA's existence window is entirely outside the query period; if strip pooling
        // applied the standalone-path clamp (per AC4, it must not), this would zero its estimate
        // and change the pool split. It must be byte-for-byte identical to the dates-unset case.
        await SeedDeviceAsync(db, pp.PowerPointId, "DeviceA", approach: ConsumptionApproach.EuLabel, euAnnualKwh: 730m,
            inUseSince: LocalMidnight(2025, 1, 1), decommissionedDate: LocalMidnight(2025, 1, 5));
        await SeedDeviceAsync(db, pp.PowerPointId, "DeviceB", approach: ConsumptionApproach.SelfMeasured, selfMeasuredKwh: 6m, selfMeasuredPeriod: SelfMeasuredPeriod.Daily);
        await SeedDailyRowAsync(db, flatId, "strip-existence", new DateOnly(2026, 1, 1), 80m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), CancellationToken.None);

        var strip = result.Rooms.Single().Devices.Single();
        strip.SubDevices.ShouldNotBeNull();
        var a = strip.SubDevices!.Single(d => d.Name == "DeviceA");
        var b = strip.SubDevices!.Single(d => d.Name == "DeviceB");
        a.Kwh.ShouldBe(20m); // 2/8 * 80 — identical to ComputeAsync_SmartPowerStripFullyConfigured_SplitsProportionallyUnchanged
        b.Kwh.ShouldBe(60m); // 6/8 * 80
    }

    [Fact]
    public async Task ComputeAsync_InUseSinceEqualsEndDate_CountsExactlyTheLastDay()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Unplugged Socket");
        await SeedDeviceAsync(db, pp.PowerPointId, "Fridge", approach: ConsumptionApproach.EuLabel, euAnnualKwh: 365m,
            inUseSince: LocalMidnight(2026, 1, 5));
        var otherPp = await SeedPowerPointAsync(db, room.RoomId, "Other Socket", plugId: "plug-other");
        await SeedDeviceAsync(db, otherPp.PowerPointId, "Other", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-other", new DateOnly(2026, 1, 1), 1m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5), CancellationToken.None);

        var device = result.Rooms.Single().Devices.Single(d => d.Name == "Fridge");
        device.Kwh.ShouldBe(1m); // InUseSince == endDate is still inclusive -> counts only Jan 5
    }

    [Fact]
    public async Task ComputeAsync_DecommissionedDateEqualsStartDate_CountsExactlyTheFirstDay()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Unplugged Socket");
        await SeedDeviceAsync(db, pp.PowerPointId, "Fridge", approach: ConsumptionApproach.EuLabel, euAnnualKwh: 365m,
            decommissionedDate: LocalMidnight(2026, 1, 1));
        var otherPp = await SeedPowerPointAsync(db, room.RoomId, "Other Socket", plugId: "plug-other");
        await SeedDeviceAsync(db, otherPp.PowerPointId, "Other", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-other", new DateOnly(2026, 1, 1), 1m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5), CancellationToken.None);

        var device = result.Rooms.Single().Devices.Single(d => d.Name == "Fridge");
        device.Kwh.ShouldBe(1m); // DecommissionedDate == startDate is still inclusive -> counts only Jan 1
    }

    [Fact]
    public async Task ComputeAsync_DeviceMidPeriodRoomMove_SplitsAcrossBothRoomsWithCorrectPartialTotals()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var roomA = await SeedRoomAsync(db, flatId, "Room A");
        var roomB = await SeedRoomAsync(db, flatId, "Room B");
        var ppA = await SeedPowerPointAsync(db, roomA.RoomId, "Socket A", plugId: "plug-a");
        var ppB = await SeedPowerPointAsync(db, roomB.RoomId, "Socket B", plugId: "plug-b");
        // Device's own current PowerPointId is ppB, matching its latest (open) assignment period.
        var device = await SeedDeviceAsync(db, ppB.PowerPointId, "Traveling Device", approach: ConsumptionApproach.None);

        await SeedAssignmentPeriodAsync(db, device.DeviceId, ppA.PowerPointId, flatId,
            LocalMidnight(2026, 1, 1), LocalMidnight(2026, 1, 2));
        await SeedAssignmentPeriodAsync(db, device.DeviceId, ppB.PowerPointId, flatId,
            LocalMidnight(2026, 1, 3), null);

        await SeedDailyRowAsync(db, flatId, "plug-a", new DateOnly(2026, 1, 1), 2m);
        await SeedDailyRowAsync(db, flatId, "plug-a", new DateOnly(2026, 1, 2), 3m);
        await SeedDailyRowAsync(db, flatId, "plug-b", new DateOnly(2026, 1, 3), 4m);
        await SeedDailyRowAsync(db, flatId, "plug-b", new DateOnly(2026, 1, 4), 1m);
        await SeedDailyRowAsync(db, flatId, "plug-b", new DateOnly(2026, 1, 5), 1m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5), CancellationToken.None);

        var deviceInA = result.Rooms.Single(r => r.RoomId == roomA.RoomId).Devices.Single(d => d.DeviceId == device.DeviceId);
        var deviceInB = result.Rooms.Single(r => r.RoomId == roomB.RoomId).Devices.Single(d => d.DeviceId == device.DeviceId);

        deviceInA.Kwh.ShouldBe(5m); // Jan 1 + Jan 2
        deviceInB.Kwh.ShouldBe(6m); // Jan 3 + Jan 4 + Jan 5
        (deviceInA.Kwh + deviceInB.Kwh).ShouldBe(11m);
        // ppA now has zero current Devices (the device moved to ppB) and would otherwise also feed
        // its Jan 1/2 kwh into the orphaned-plug fallback, double-counting it into TotalKwh.
        result.TotalKwh.ShouldBe(11m);
    }

    [Fact]
    public async Task ComputeAsync_DeviceMovedAwayFromPowerPointWithOtherUnclaimedDays_OnlyUnclaimedDaysCountAsOrphaned()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var roomA = await SeedRoomAsync(db, flatId, "Room A");
        var roomB = await SeedRoomAsync(db, flatId, "Room B");
        var ppA = await SeedPowerPointAsync(db, roomA.RoomId, "Socket A", plugId: "plug-a");
        var ppB = await SeedPowerPointAsync(db, roomB.RoomId, "Socket B", plugId: "plug-b");
        var device = await SeedDeviceAsync(db, ppB.PowerPointId, "Traveling Device", approach: ConsumptionApproach.None);

        // Device only claims ppA for Jan 1 — Jan 2's plug-a reading has no device history behind it
        // at all (e.g. ppA sat briefly empty) and must still surface as orphaned kwh (AC10), not be
        // silently dropped by the double-count fix.
        await SeedAssignmentPeriodAsync(db, device.DeviceId, ppA.PowerPointId, flatId,
            LocalMidnight(2026, 1, 1), LocalMidnight(2026, 1, 2));
        await SeedAssignmentPeriodAsync(db, device.DeviceId, ppB.PowerPointId, flatId,
            LocalMidnight(2026, 1, 2), null);

        await SeedDailyRowAsync(db, flatId, "plug-a", new DateOnly(2026, 1, 1), 2m);
        await SeedDailyRowAsync(db, flatId, "plug-a", new DateOnly(2026, 1, 2), 9m); // unclaimed by any device
        await SeedDailyRowAsync(db, flatId, "plug-b", new DateOnly(2026, 1, 2), 4m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), CancellationToken.None);

        var deviceInA = result.Rooms.Single(r => r.RoomId == roomA.RoomId).Devices.Single(d => d.DeviceId == device.DeviceId);
        deviceInA.Kwh.ShouldBe(2m); // Jan 1 only
        result.TotalKwh.ShouldBe(15m); // 2 (deviceA, Jan1) + 4 (deviceB, Jan2) + 9 (orphaned plug-a, Jan2)
    }

    [Fact]
    public async Task ComputeAsync_ResolvedPowerPointNoLongerExists_FallsBackToCurrentPowerPointWithoutLosingKwh()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket", plugId: "plug-1");
        var device = await SeedDeviceAsync(db, pp.PowerPointId, "Device", approach: ConsumptionApproach.None);

        // Simulates a PowerPoint that's since been deleted — the historical period still points at
        // a PowerPointId absent from the current structure snapshot.
        var deletedPowerPointId = Guid.NewGuid();
        await SeedAssignmentPeriodAsync(db, device.DeviceId, deletedPowerPointId, flatId, LocalMidnight(2026, 1, 1), null);

        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 1), 5m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), CancellationToken.None);

        var deviceResult = result.Rooms.Single().Devices.Single(d => d.DeviceId == device.DeviceId);
        deviceResult.Kwh.ShouldBe(5m);
        deviceResult.PowerPointId.ShouldBe(pp.PowerPointId);
    }

    [Fact]
    public async Task ComputeAsync_ResolvedPowerPointIsNowASmartStrip_FallsBackInsteadOfDoubleCountingPlugKwh()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var roomA = await SeedRoomAsync(db, flatId, "Room A");
        var roomB = await SeedRoomAsync(db, flatId, "Room B");
        var ppX = await SeedPowerPointAsync(db, roomA.RoomId, "Socket X", plugId: "plug-x");
        var deviceX = await SeedDeviceAsync(db, ppX.PowerPointId, "DeviceX", approach: ConsumptionApproach.None);
        var ppStrip = await SeedPowerPointAsync(db, roomB.RoomId, "Strip", plugId: "strip-1");
        await SeedDeviceAsync(db, ppStrip.PowerPointId, "DeviceY", approach: ConsumptionApproach.EuLabel, euAnnualKwh: 730m); // daily = 2
        await SeedDeviceAsync(db, ppStrip.PowerPointId, "DeviceZ", approach: ConsumptionApproach.SelfMeasured, selfMeasuredKwh: 6m, selfMeasuredPeriod: SelfMeasuredPeriod.Daily);
        await SeedDailyRowAsync(db, flatId, "strip-1", new DateOnly(2026, 1, 1), 80m);

        // DeviceX's entire history says it was on ppStrip — but ppStrip is *currently* a Smart Power
        // Strip shared by DeviceY/DeviceZ. The day-by-day path must not attribute ppStrip's full raw
        // plug kwh to DeviceX on top of the strip's own pool-math share for DeviceY/DeviceZ.
        await SeedAssignmentPeriodAsync(db, deviceX.DeviceId, ppStrip.PowerPointId, flatId, LocalMidnight(2026, 1, 1), null);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), CancellationToken.None);

        var stripDecomposition = result.Rooms.Single(r => r.RoomId == roomB.RoomId).Devices.Single();
        stripDecomposition.IsSmartStrip.ShouldBeTrue();
        stripDecomposition.Kwh.ShouldBe(80m);

        var deviceXResult = result.Rooms.Single(r => r.RoomId == roomA.RoomId).Devices.Single(d => d.DeviceId == deviceX.DeviceId);
        deviceXResult.Kwh.ShouldBe(0m); // ppX (its current PowerPoint) has no daily data seeded — falls back, doesn't inherit the strip's 80
        result.Rooms.Sum(r => r.Kwh).ShouldBe(80m); // not 160 — the strip's kwh must not be counted twice
    }

    [Fact]
    public async Task ComputeAsync_SmartPowerStripSubDeviceWithAssignmentPeriod_PoolMathUnaffected()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var roomA = await SeedRoomAsync(db, flatId, "Room A");
        var roomB = await SeedRoomAsync(db, flatId, "Room B");
        var pp = await SeedPowerPointAsync(db, roomA.RoomId, "Strip", plugId: "strip-1");
        var deviceA = await SeedDeviceAsync(db, pp.PowerPointId, "DeviceA", approach: ConsumptionApproach.EuLabel, euAnnualKwh: 730m); // daily = 2
        await SeedDeviceAsync(db, pp.PowerPointId, "DeviceB", approach: ConsumptionApproach.SelfMeasured, selfMeasuredKwh: 6m, selfMeasuredPeriod: SelfMeasuredPeriod.Daily);
        await SeedDailyRowAsync(db, flatId, "strip-1", new DateOnly(2026, 1, 1), 80m);

        // Even though a period claims DeviceA moved to a Power Point in Room B, sub-device pool
        // math (AC7) must ignore assignment-period history entirely and keep using the strip's
        // current, structural Room.
        var otherPpInRoomB = await SeedPowerPointAsync(db, roomB.RoomId, "Unrelated Socket");
        await SeedAssignmentPeriodAsync(db, deviceA.DeviceId, otherPpInRoomB.PowerPointId, flatId, LocalMidnight(2026, 1, 1), null);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), CancellationToken.None);

        var stripInRoomA = result.Rooms.Single(r => r.RoomId == roomA.RoomId).Devices.Single();
        stripInRoomA.IsSmartStrip.ShouldBeTrue();
        stripInRoomA.Kwh.ShouldBe(80m);
        stripInRoomA.SubDevices!.Single(d => d.Name == "DeviceA").Kwh.ShouldBe(20m); // unchanged pool math (2/8 * 80)
        result.Rooms.Single(r => r.RoomId == roomB.RoomId).Devices.ShouldBeEmpty();
    }

    [Fact]
    public async Task ComputeAsync_NonTerminatingDecimalDivision_RoundsToExactlyFourDecimalPlaces()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Unplugged Socket");
        await SeedDeviceAsync(db, pp.PowerPointId, "Lamp", approach: ConsumptionApproach.SelfMeasured, selfMeasuredKwh: 10m, selfMeasuredPeriod: SelfMeasuredPeriod.Weekly);
        var otherPp = await SeedPowerPointAsync(db, room.RoomId, "Other Socket", plugId: "plug-other");
        await SeedDeviceAsync(db, otherPp.PowerPointId, "Other", approach: ConsumptionApproach.None);
        await SeedDailyRowAsync(db, flatId, "plug-other", new DateOnly(2026, 1, 1), 1m);

        var result = await MakeEngine(db).ComputeAsync(flatId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), CancellationToken.None);

        var device = result.Rooms.Single().Devices.Single(d => d.Name == "Lamp");
        var expected = Math.Round(10m / 7m * 3m, 4, MidpointRounding.AwayFromZero);
        device.Kwh.ShouldBe(expected);
        device.Kwh.ShouldNotBe(10m / 7m * 3m); // proves rounding actually happened, not a pass-through
    }
}
