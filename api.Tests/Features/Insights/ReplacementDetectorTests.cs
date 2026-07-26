using System.Text.Json;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Insights;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api.Tests.Features.Insights;

public class ReplacementDetectorTests
{
    private static AppDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

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
        string? euLabelClass = null,
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
            EuLabelClass = euLabelClass,
            EuAnnualKwh = euAnnualKwh,
            SelfMeasuredKwh = selfMeasuredKwh,
            SelfMeasuredPeriod = selfMeasuredPeriod
        };
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        return device;
    }

    private static async Task SeedDailyRowAsync(AppDbContext db, Guid flatId, string plugId, DateOnly date, decimal kwh)
    {
        db.SmartPlugDailyData.Add(new SmartPlugDailyData { FlatId = flatId, PlugId = plugId, Date = date, KwhValue = kwh });
        await db.SaveChangesAsync();
    }

    private static async Task Seed7DailyRowsAsync(AppDbContext db, Guid flatId, string plugId, decimal kwhPerDay)
    {
        for (var i = 1; i <= 7; i++)
            await SeedDailyRowAsync(db, flatId, plugId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i)), kwhPerDay);
    }

    private static async Task SeedTariffAsync(AppDbContext db, Guid flatId, decimal pricePerKwh)
    {
        db.Tariffs.Add(new Tariff
        {
            FlatId = flatId,
            ContractStartDate = DateTimeOffset.UtcNow.AddYears(-1),
            PricePerKwh = pricePerKwh,
            MonthlyBaseFee = 0m
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task DetectAsync_HighConsumptionDeviceClassC_WritesInsightWithSuggestedClassAndSavings()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var highPp = await SeedPowerPointAsync(db, room.RoomId, "High");
        var highDevice = await SeedDeviceAsync(db, highPp.PowerPointId, "Old Fridge",
            approach: ConsumptionApproach.EuLabel, euLabelClass: "C", euAnnualKwh: 1000m);
        var lowPp = await SeedPowerPointAsync(db, room.RoomId, "Low");
        await SeedDeviceAsync(db, lowPp.PowerPointId, "Small Lamp",
            approach: ConsumptionApproach.EuLabel, euLabelClass: "C", euAnnualKwh: 10m);
        await SeedTariffAsync(db, flatId, pricePerKwh: 0.30m);

        await new ReplacementDetector(db).DetectAsync(flatId, Guid.NewGuid(), CancellationToken.None);

        var insight = await db.Insights.SingleAsync();
        insight.Type.ShouldBe(InsightType.Replacement);
        insight.DeviceId.ShouldBe(highDevice.DeviceId);

        using var json = JsonDocument.Parse(insight.Data);
        json.RootElement.GetProperty("deviceName").GetString().ShouldBe("Old Fridge");
        json.RootElement.GetProperty("estimatedAnnualKwh").GetDecimal().ShouldBe(1000m);
        json.RootElement.GetProperty("estimatedAnnualCost").GetDecimal().ShouldBe(300m);
        json.RootElement.GetProperty("suggestedClass").GetString().ShouldBe("B");
        json.RootElement.GetProperty("estimatedSavingsEur").GetDecimal().ShouldBe(45m);
    }

    [Fact]
    public async Task DetectAsync_LegacyTopGradeClass_NotFlaggedDespiteHighConsumption()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket");
        await SeedDeviceAsync(db, pp.PowerPointId, "Efficient Fridge",
            approach: ConsumptionApproach.EuLabel, euLabelClass: "A+++", euAnnualKwh: 1000m);
        await SeedTariffAsync(db, flatId, pricePerKwh: 0.30m);

        await new ReplacementDetector(db).DetectAsync(flatId, Guid.NewGuid(), CancellationToken.None);

        (await db.Insights.CountAsync()).ShouldBe(0);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    public async Task DetectAsync_ClassAOrB_WritesNoInsight(string euLabelClass)
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket");
        await SeedDeviceAsync(db, pp.PowerPointId, "New Washer",
            approach: ConsumptionApproach.EuLabel, euLabelClass: euLabelClass, euAnnualKwh: 1000m);
        await SeedTariffAsync(db, flatId, pricePerKwh: 0.30m);

        await new ReplacementDetector(db).DetectAsync(flatId, Guid.NewGuid(), CancellationToken.None);

        (await db.Insights.CountAsync()).ShouldBe(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-real-class")]
    public async Task DetectAsync_UnrecognizedOrBlankEuLabelClass_ExcludedWithoutError(string euLabelClass)
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket");
        await SeedDeviceAsync(db, pp.PowerPointId, "Mystery Device",
            approach: ConsumptionApproach.EuLabel, euLabelClass: euLabelClass, euAnnualKwh: 1000m);
        await SeedTariffAsync(db, flatId, pricePerKwh: 0.30m);

        await Should.NotThrowAsync(() => new ReplacementDetector(db).DetectAsync(flatId, Guid.NewGuid(), CancellationToken.None));

        (await db.Insights.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DetectAsync_DeviceOutsideTop20PercentBand_WritesNoInsight()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);

        // 10 devices: the 9 low-consumption ones push the 1 C-class device below the
        // ceil(10 * 0.2) = 2 top-consumption band, so it must not be flagged.
        for (var i = 0; i < 9; i++)
        {
            var pp = await SeedPowerPointAsync(db, room.RoomId, $"High-{i}");
            await SeedDeviceAsync(db, pp.PowerPointId, $"High Consumer {i}",
                approach: ConsumptionApproach.EuLabel, euLabelClass: "A", euAnnualKwh: 1000m);
        }
        var lowPp = await SeedPowerPointAsync(db, room.RoomId, "Low");
        await SeedDeviceAsync(db, lowPp.PowerPointId, "Old Toaster",
            approach: ConsumptionApproach.EuLabel, euLabelClass: "C", euAnnualKwh: 5m);
        await SeedTariffAsync(db, flatId, pricePerKwh: 0.30m);

        await new ReplacementDetector(db).DetectAsync(flatId, Guid.NewGuid(), CancellationToken.None);

        (await db.Insights.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DetectAsync_NoDevicesWithComputableConsumption_WritesNoInsights()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket");
        await SeedDeviceAsync(db, pp.PowerPointId, "Unconfigured Device", approach: ConsumptionApproach.None, euLabelClass: "C");
        await SeedTariffAsync(db, flatId, pricePerKwh: 0.30m);

        await new ReplacementDetector(db).DetectAsync(flatId, Guid.NewGuid(), CancellationToken.None);

        (await db.Insights.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DetectAsync_MeasuredSource_ComputesCorrectAnnualKwh()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket", plugId: "plug-1");
        var device = await SeedDeviceAsync(db, pp.PowerPointId, "Old Freezer", euLabelClass: "C");
        await Seed7DailyRowsAsync(db, flatId, "plug-1", kwhPerDay: 2m);
        await SeedTariffAsync(db, flatId, pricePerKwh: 0.30m);

        await new ReplacementDetector(db).DetectAsync(flatId, Guid.NewGuid(), CancellationToken.None);

        var insight = await db.Insights.SingleAsync(i => i.DeviceId == device.DeviceId);
        using var json = JsonDocument.Parse(insight.Data);
        json.RootElement.GetProperty("estimatedAnnualKwh").GetDecimal().ShouldBe(730m); // 2 * 365
    }

    [Fact]
    public async Task DetectAsync_EuLabelSource_ComputesCorrectAnnualKwh()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket");
        var device = await SeedDeviceAsync(db, pp.PowerPointId, "Old Dryer",
            approach: ConsumptionApproach.EuLabel, euLabelClass: "C", euAnnualKwh: 500m);
        await SeedTariffAsync(db, flatId, pricePerKwh: 0.30m);

        await new ReplacementDetector(db).DetectAsync(flatId, Guid.NewGuid(), CancellationToken.None);

        var insight = await db.Insights.SingleAsync(i => i.DeviceId == device.DeviceId);
        using var json = JsonDocument.Parse(insight.Data);
        json.RootElement.GetProperty("estimatedAnnualKwh").GetDecimal().ShouldBe(500m);
    }

    [Fact]
    public async Task DetectAsync_SelfMeasuredWeeklySource_ComputesCorrectAnnualKwh()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket");
        var device = await SeedDeviceAsync(db, pp.PowerPointId, "Old Dishwasher",
            approach: ConsumptionApproach.SelfMeasured, euLabelClass: "C",
            selfMeasuredKwh: 10m, selfMeasuredPeriod: SelfMeasuredPeriod.Weekly);
        await SeedTariffAsync(db, flatId, pricePerKwh: 0.30m);

        await new ReplacementDetector(db).DetectAsync(flatId, Guid.NewGuid(), CancellationToken.None);

        var insight = await db.Insights.SingleAsync(i => i.DeviceId == device.DeviceId);
        using var json = JsonDocument.Parse(insight.Data);
        json.RootElement.GetProperty("estimatedAnnualKwh").GetDecimal().ShouldBe(520m); // 10 * 52
    }

    [Fact]
    public async Task DetectAsync_SelfMeasuredDailySource_ComputesCorrectAnnualKwh()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket");
        var device = await SeedDeviceAsync(db, pp.PowerPointId, "Old Space Heater",
            approach: ConsumptionApproach.SelfMeasured, euLabelClass: "C",
            selfMeasuredKwh: 2m, selfMeasuredPeriod: SelfMeasuredPeriod.Daily);
        await SeedTariffAsync(db, flatId, pricePerKwh: 0.30m);

        await new ReplacementDetector(db).DetectAsync(flatId, Guid.NewGuid(), CancellationToken.None);

        var insight = await db.Insights.SingleAsync(i => i.DeviceId == device.DeviceId);
        using var json = JsonDocument.Parse(insight.Data);
        json.RootElement.GetProperty("estimatedAnnualKwh").GetDecimal().ShouldBe(730m); // 2 * 365
    }

    [Fact]
    public async Task DetectAsync_MeasuredDataInsufficientWithValidEuLabelConfig_FallsBackToApproachAnnualKwh()
    {
        // Regresses the Story 10.2 review fix: a single-device PowerPoint with a PlugId but
        // fewer than MinDistinctDays of SmartPlugDailyData must fall back to the device's
        // EuLabel/SelfMeasured approach figure instead of being excluded outright.
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket", plugId: "plug-1");
        var device = await SeedDeviceAsync(db, pp.PowerPointId, "Old Freezer",
            approach: ConsumptionApproach.EuLabel, euLabelClass: "C", euAnnualKwh: 500m);
        for (var i = 1; i <= 3; i++) // 3 < MinDistinctDays (7) -> measured branch returns null
            await SeedDailyRowAsync(db, flatId, "plug-1", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i)), kwh: 2m);
        await SeedTariffAsync(db, flatId, pricePerKwh: 0.30m);

        await new ReplacementDetector(db).DetectAsync(flatId, Guid.NewGuid(), CancellationToken.None);

        var insight = await db.Insights.SingleAsync(i => i.DeviceId == device.DeviceId);
        using var json = JsonDocument.Parse(insight.Data);
        json.RootElement.GetProperty("estimatedAnnualKwh").GetDecimal().ShouldBe(500m);
    }

    [Fact]
    public async Task DetectAsync_MultiDevicePowerPoint_StillEligibleViaEuLabel()
    {
        // Intentional: the 1:1-device constraint only applies to the *measured* (smart-plug)
        // source per AC #4's literal wording — EuLabel/SelfMeasured devices behind a
        // multi-device PowerPoint (smart strip) remain eligible for replacement detection.
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var stripPp = await SeedPowerPointAsync(db, room.RoomId, "Strip", plugId: "plug-1");
        var device = await SeedDeviceAsync(db, stripPp.PowerPointId, "Old Console",
            approach: ConsumptionApproach.EuLabel, euLabelClass: "C", euAnnualKwh: 1000m);
        await SeedDeviceAsync(db, stripPp.PowerPointId, "Other Strip Device",
            approach: ConsumptionApproach.EuLabel, euLabelClass: "A", euAnnualKwh: 10m);
        await SeedTariffAsync(db, flatId, pricePerKwh: 0.30m);

        await new ReplacementDetector(db).DetectAsync(flatId, Guid.NewGuid(), CancellationToken.None);

        var insight = await db.Insights.SingleAsync();
        insight.DeviceId.ShouldBe(device.DeviceId);
    }
}
