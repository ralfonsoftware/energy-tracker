using System.Globalization;
using System.Text.Json;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Insights;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api.Tests.Features.Insights;

public class StandbyDetectorTests
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

    private static async Task<Device> SeedDeviceAsync(AppDbContext db, Guid powerPointId, string name)
    {
        var device = new Device { DeviceId = Guid.NewGuid(), PowerPointId = powerPointId, Name = name };
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        return device;
    }

    private static async Task SeedIntervalRowAsync(AppDbContext db, Guid flatId, string plugId, DateTimeOffset timestamp, decimal whValue)
    {
        db.SmartPlugIntervalData.Add(new SmartPlugIntervalData
        {
            Id = Guid.NewGuid(),
            FlatId = flatId,
            PlugId = plugId,
            Timestamp = timestamp,
            WhValue = whValue
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedDailyRowAsync(AppDbContext db, Guid flatId, string plugId, DateOnly date, decimal kwh)
    {
        db.SmartPlugDailyData.Add(new SmartPlugDailyData { FlatId = flatId, PlugId = plugId, Date = date, KwhValue = kwh });
        await db.SaveChangesAsync();
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

    // Zero offset matches EveHomeParser's synthetic wall-clock convention for this field.
    private static DateTimeOffset NightTimestamp(int daysAgo, int hour = 23) =>
        new(DateTime.UtcNow.Date.AddDays(-daysAgo).AddHours(hour), TimeSpan.Zero);

    private static async Task Seed7NightRowsAsync(AppDbContext db, Guid flatId, string plugId, decimal whValuePerRow)
    {
        for (var i = 1; i <= 7; i++)
            await SeedIntervalRowAsync(db, flatId, plugId, NightTimestamp(i), whValuePerRow);
    }

    [Fact]
    public async Task DetectAsync_MeanStandbyAboveThresholdWithSevenDays_WritesInsightWithCorrectData()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket", plugId: "plug-1");
        var device = await SeedDeviceAsync(db, pp.PowerPointId, "Games Console");
        // 1 Wh per 10-min interval => 6 W, above the 2 W threshold.
        await Seed7NightRowsAsync(db, flatId, "plug-1", whValuePerRow: 1m);
        await SeedTariffAsync(db, flatId, pricePerKwh: 0.30m);
        var runId = Guid.NewGuid();

        await new StandbyDetector(db).DetectAsync(flatId, runId, CancellationToken.None);

        var insight = await db.Insights.SingleAsync();
        insight.Type.ShouldBe(InsightType.Standby);
        insight.DeviceId.ShouldBe(device.DeviceId);
        insight.FlatId.ShouldBe(flatId);
        insight.RunId.ShouldBe(runId);

        using var json = JsonDocument.Parse(insight.Data);
        json.RootElement.GetProperty("deviceName").GetString().ShouldBe("Games Console");
        json.RootElement.GetProperty("meanStandbyWatts").GetDecimal().ShouldBe(6m);
        // (6W / 1000) * 10h * 30 days = 1.8 kWh; cost = 1.8 * 0.30 = 0.54
        json.RootElement.GetProperty("estimatedMonthlyKwh").GetDecimal().ShouldBe(1.8m);
        json.RootElement.GetProperty("estimatedMonthlyCost").GetDecimal().ShouldBe(0.54m);
    }

    [Fact]
    public async Task DetectAsync_MeanStandbyBelowThreshold_WritesNoInsight()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket", plugId: "plug-1");
        await SeedDeviceAsync(db, pp.PowerPointId, "Lamp");
        // 0.2 Wh per 10-min interval => 1.2 W, below the 2 W threshold.
        await Seed7NightRowsAsync(db, flatId, "plug-1", whValuePerRow: 0.2m);
        await SeedTariffAsync(db, flatId, pricePerKwh: 0.30m);

        await new StandbyDetector(db).DetectAsync(flatId, Guid.NewGuid(), CancellationToken.None);

        (await db.Insights.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DetectAsync_MerossOnlyDeviceNoIntervalData_ExcludedWithoutError()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket", plugId: "plug-1");
        await SeedDeviceAsync(db, pp.PowerPointId, "Fridge");
        // Meross-format devices only ever produce SmartPlugDailyData, never interval rows.
        for (var i = 1; i <= 10; i++)
            await SeedDailyRowAsync(db, flatId, "plug-1", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i)), kwh: 1m);
        await SeedTariffAsync(db, flatId, pricePerKwh: 0.30m);

        await Should.NotThrowAsync(() => new StandbyDetector(db).DetectAsync(flatId, Guid.NewGuid(), CancellationToken.None));

        (await db.Insights.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DetectAsync_FewerThanSevenDistinctDays_WritesNoInsight()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket", plugId: "plug-1");
        await SeedDeviceAsync(db, pp.PowerPointId, "Games Console");
        for (var i = 1; i <= 5; i++)
            await SeedIntervalRowAsync(db, flatId, "plug-1", NightTimestamp(i), whValue: 1m);
        await SeedTariffAsync(db, flatId, pricePerKwh: 0.30m);

        await new StandbyDetector(db).DetectAsync(flatId, Guid.NewGuid(), CancellationToken.None);

        (await db.Insights.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DetectAsync_NoResolvableTariff_WritesNoInsight()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket", plugId: "plug-1");
        await SeedDeviceAsync(db, pp.PowerPointId, "Games Console");
        await Seed7NightRowsAsync(db, flatId, "plug-1", whValuePerRow: 1m);
        // No tariff seeded at all.

        await new StandbyDetector(db).DetectAsync(flatId, Guid.NewGuid(), CancellationToken.None);

        (await db.Insights.CountAsync()).ShouldBe(0);
    }

    private static async Task SeedExistingInsightAsync(AppDbContext db, Guid flatId, Guid deviceId, decimal estimatedMonthlyCost, DateTimeOffset? createdAt = null)
    {
        db.Insights.Add(new Insight
        {
            InsightId = Guid.NewGuid(),
            FlatId = flatId,
            Type = InsightType.Standby,
            DeviceId = deviceId,
            Data = $$"""{"estimatedMonthlyCost":{{estimatedMonthlyCost.ToString(CultureInfo.InvariantCulture)}}}""",
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task DetectAsync_WithinFivePercentOfMostRecentStoredInsight_WritesNoNewInsight()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket", plugId: "plug-1");
        var device = await SeedDeviceAsync(db, pp.PowerPointId, "Games Console");
        // 1 Wh per 10-min interval => 6 W => estimatedMonthlyCost = 0.54 (see first test in this file).
        await Seed7NightRowsAsync(db, flatId, "plug-1", whValuePerRow: 1m);
        await SeedTariffAsync(db, flatId, pricePerKwh: 0.30m);
        // 0.55 is within 5% of 0.54.
        await SeedExistingInsightAsync(db, flatId, device.DeviceId, estimatedMonthlyCost: 0.55m);

        await new StandbyDetector(db).DetectAsync(flatId, Guid.NewGuid(), CancellationToken.None);

        (await db.Insights.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task DetectAsync_BeyondFivePercentOfMostRecentStoredInsight_WritesNewInsightAlongsideUntouchedPrior()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Socket", plugId: "plug-1");
        var device = await SeedDeviceAsync(db, pp.PowerPointId, "Games Console");
        // 1 Wh per 10-min interval => 6 W => estimatedMonthlyCost = 0.54 (see first test in this file).
        await Seed7NightRowsAsync(db, flatId, "plug-1", whValuePerRow: 1m);
        await SeedTariffAsync(db, flatId, pricePerKwh: 0.30m);
        // 1.00 is well beyond 5% of 0.54.
        await SeedExistingInsightAsync(db, flatId, device.DeviceId, estimatedMonthlyCost: 1.00m);
        var priorInsightId = (await db.Insights.SingleAsync()).InsightId;

        await new StandbyDetector(db).DetectAsync(flatId, Guid.NewGuid(), CancellationToken.None);

        (await db.Insights.CountAsync()).ShouldBe(2);
        var prior = await db.Insights.SingleAsync(i => i.InsightId == priorInsightId);
        using var priorJson = JsonDocument.Parse(prior.Data);
        priorJson.RootElement.GetProperty("estimatedMonthlyCost").GetDecimal().ShouldBe(1.00m);
    }

    [Fact]
    public async Task DetectAsync_MultiDeviceSmartStripPowerPoint_ExcludedFromDetection()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var room = await SeedRoomAsync(db, flatId);
        var pp = await SeedPowerPointAsync(db, room.RoomId, "Power Strip", plugId: "plug-1");
        await SeedDeviceAsync(db, pp.PowerPointId, "Device A");
        await SeedDeviceAsync(db, pp.PowerPointId, "Device B");
        await Seed7NightRowsAsync(db, flatId, "plug-1", whValuePerRow: 1m);
        await SeedTariffAsync(db, flatId, pricePerKwh: 0.30m);

        await new StandbyDetector(db).DetectAsync(flatId, Guid.NewGuid(), CancellationToken.None);

        (await db.Insights.CountAsync()).ShouldBe(0);
    }
}
