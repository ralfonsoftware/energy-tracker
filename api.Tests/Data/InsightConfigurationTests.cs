using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api.Tests.Data;

public class InsightConfigurationTests
{
    private static AppDbContext MakeDb(string? dbName = null) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options);

    private static Flat MakeFlat() => new()
    {
        FlatId = Guid.NewGuid(),
        UserId = "owner-user",
        Name = "Test Flat",
        AnnualKwhBaseline = 3500m,
        SpikeThreshold = 2.0m
    };

    private static InsightRun MakeInsightRun(Guid flatId) => new()
    {
        RunId = Guid.NewGuid(),
        FlatId = flatId,
        Status = InsightRunStatus.Complete,
        StartedAt = DateTimeOffset.UtcNow
    };

    private static Insight MakeInsight(Guid flatId, Guid? runId, Guid? deviceId = null) => new()
    {
        InsightId = Guid.NewGuid(),
        FlatId = flatId,
        RunId = runId,
        DeviceId = deviceId,
        Type = InsightType.Standby,
        Data = "{}",
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task DeletingInsightRun_NullsRunId_WithoutDeletingInsight()
    {
        var dbName = Guid.NewGuid().ToString();
        var flat = MakeFlat();
        var insightRun = MakeInsightRun(flat.FlatId);
        var insight = MakeInsight(flat.FlatId, insightRun.RunId);

        using (var seedDb = MakeDb(dbName))
        {
            seedDb.Flats.Add(flat);
            seedDb.InsightRuns.Add(insightRun);
            seedDb.Insights.Add(insight);
            await seedDb.SaveChangesAsync();
        }

        using var db = MakeDb(dbName);
        var trackedRun = await db.InsightRuns.SingleAsync(r => r.RunId == insightRun.RunId);
        await db.Insights.Where(i => i.RunId == insightRun.RunId).LoadAsync();
        db.InsightRuns.Remove(trackedRun);
        await db.SaveChangesAsync();

        var survivingInsight = await db.Insights.SingleOrDefaultAsync(i => i.InsightId == insight.InsightId);
        survivingInsight.ShouldNotBeNull();
        survivingInsight.RunId.ShouldBeNull();
    }

    [Fact]
    public async Task DeletingDevice_NullsDeviceId_WithoutDeletingInsight()
    {
        var dbName = Guid.NewGuid().ToString();
        var flat = MakeFlat();
        var room = new Room { RoomId = Guid.NewGuid(), FlatId = flat.FlatId, Name = "Room", SortOrder = 0 };
        var powerPoint = new PowerPoint { PowerPointId = Guid.NewGuid(), RoomId = room.RoomId, Name = "Socket" };
        var device = new Device { DeviceId = Guid.NewGuid(), PowerPointId = powerPoint.PowerPointId, Name = "Device", ConsumptionApproach = ConsumptionApproach.None };
        var insight = MakeInsight(flat.FlatId, runId: null, deviceId: device.DeviceId);

        using (var seedDb = MakeDb(dbName))
        {
            seedDb.Flats.Add(flat);
            seedDb.Rooms.Add(room);
            seedDb.PowerPoints.Add(powerPoint);
            seedDb.Devices.Add(device);
            seedDb.Insights.Add(insight);
            await seedDb.SaveChangesAsync();
        }

        using var db = MakeDb(dbName);
        var trackedDevice = await db.Devices.SingleAsync(d => d.DeviceId == device.DeviceId);
        await db.Insights.Where(i => i.DeviceId == device.DeviceId).LoadAsync();
        db.Devices.Remove(trackedDevice);
        await db.SaveChangesAsync();

        var survivingInsight = await db.Insights.SingleOrDefaultAsync(i => i.InsightId == insight.InsightId);
        survivingInsight.ShouldNotBeNull();
        survivingInsight.DeviceId.ShouldBeNull();
    }
}
