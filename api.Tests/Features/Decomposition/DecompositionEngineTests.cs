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
        SelfMeasuredPeriod? selfMeasuredPeriod = null)
    {
        var device = new Device
        {
            DeviceId = Guid.NewGuid(),
            PowerPointId = powerPointId,
            Name = name,
            ConsumptionApproach = approach,
            EuAnnualKwh = euAnnualKwh,
            SelfMeasuredKwh = selfMeasuredKwh,
            SelfMeasuredPeriod = selfMeasuredPeriod
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
}
