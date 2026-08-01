using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api.Tests.Integration;

public class FlatCascadeDeleteTests : SqliteIntegrationTestBase
{
    [Fact]
    public async Task FlatDelete_CascadesAcrossAllTenDependentTables_NoOrphansOrFkViolations()
    {
        using var db = CreateContext();

        // An untouched second flat + its own child rows, to prove the cascade is scoped to the
        // deleted flat's FlatId and doesn't over-delete unrelated data.
        var otherUser = new User { UserId = "user-untouched" };
        var otherFlat = new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = otherUser.UserId,
            Name = "Untouched Flat",
            AnnualKwhBaseline = 1000m,
            SpikeThreshold = 2.0m
        };
        var otherMeterReading = new MeterReading
        {
            ReadingId = Guid.NewGuid(),
            FlatId = otherFlat.FlatId,
            KwhValue = 50m,
            ReadingDate = DateTimeOffset.UtcNow
        };
        db.Users.Add(otherUser);
        db.Flats.Add(otherFlat);
        db.MeterReadings.Add(otherMeterReading);

        var user = new User { UserId = "user-cascade-test" };
        var flat = new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = user.UserId,
            Name = "Cascade Test Flat",
            AnnualKwhBaseline = 3500m,
            SpikeThreshold = 2.0m
        };
        db.Users.Add(user);
        db.Flats.Add(flat);

        var meterReading = new MeterReading
        {
            ReadingId = Guid.NewGuid(),
            FlatId = flat.FlatId,
            KwhValue = 100m,
            ReadingDate = DateTimeOffset.UtcNow
        };
        db.MeterReadings.Add(meterReading);

        var tariff = new Tariff
        {
            TariffId = Guid.NewGuid(),
            FlatId = flat.FlatId,
            PricePerKwh = 0.35m,
            MonthlyBaseFee = 10m,
            ContractStartDate = DateTimeOffset.UtcNow.AddMonths(-6)
        };
        db.Tariffs.Add(tariff);

        var room = new Room
        {
            RoomId = Guid.NewGuid(),
            FlatId = flat.FlatId,
            Name = "Living Room",
            SortOrder = 1
        };
        db.Rooms.Add(room);

        var powerPoint = new PowerPoint
        {
            PowerPointId = Guid.NewGuid(),
            RoomId = room.RoomId,
            FlatId = flat.FlatId,
            Name = "Socket 1"
        };
        db.PowerPoints.Add(powerPoint);

        var device = new Device
        {
            DeviceId = Guid.NewGuid(),
            PowerPointId = powerPoint.PowerPointId,
            Name = "Fridge",
            ConsumptionApproach = ConsumptionApproach.None
        };
        db.Devices.Add(device);

        var importJob = new ImportJob
        {
            ImportJobId = Guid.NewGuid(),
            FlatId = flat.FlatId,
            PlugId = "plug-1",
            OriginalFileName = "import.csv",
            Status = ImportStatus.Complete,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ImportJobs.Add(importJob);

        var dailyData = new SmartPlugDailyData
        {
            Id = Guid.NewGuid(),
            PlugId = "plug-1",
            FlatId = flat.FlatId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            KwhValue = 1.5m,
            IsInterpolated = false
        };
        db.SmartPlugDailyData.Add(dailyData);

        var intervalData = new SmartPlugIntervalData
        {
            Id = Guid.NewGuid(),
            PlugId = "plug-1",
            FlatId = flat.FlatId,
            Timestamp = DateTimeOffset.UtcNow,
            WhValue = 200m
        };
        db.SmartPlugIntervalData.Add(intervalData);

        var insightRun = new InsightRun
        {
            RunId = Guid.NewGuid(),
            FlatId = flat.FlatId,
            Status = InsightRunStatus.Complete,
            StartedAt = DateTimeOffset.UtcNow
        };
        db.InsightRuns.Add(insightRun);

        var insight = new Insight
        {
            InsightId = Guid.NewGuid(),
            FlatId = flat.FlatId,
            RunId = insightRun.RunId,
            DeviceId = device.DeviceId,
            Type = InsightType.Standby,
            Data = "{}",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Insights.Add(insight);

        await db.SaveChangesAsync();

        // Delete through a fresh context that only loads the target Flat, then calls the exact
        // production helper (AppDbContextExtensions.LoadFlatCascadeChildrenAsync, used by
        // DeleteFlatFunction) to pull children into the tracker before removing — mirroring
        // production's real code path rather than relying on entities already being tracked.
        using (var deleteDb = CreateContext())
        {
            var trackedFlat = await deleteDb.Flats.SingleAsync(f => f.FlatId == flat.FlatId);
            await deleteDb.LoadFlatCascadeChildrenAsync(flat.FlatId, CancellationToken.None);
            deleteDb.Flats.Remove(trackedFlat);
            var act = async () => await deleteDb.SaveChangesAsync();
            await Should.NotThrowAsync(act);
        }

        using var verifyDb = CreateContext();
        (await verifyDb.Flats.AnyAsync(f => f.FlatId == flat.FlatId)).ShouldBeFalse();
        (await verifyDb.MeterReadings.AnyAsync(r => r.ReadingId == meterReading.ReadingId)).ShouldBeFalse();
        (await verifyDb.Tariffs.AnyAsync(t => t.TariffId == tariff.TariffId)).ShouldBeFalse();
        (await verifyDb.Rooms.AnyAsync(r => r.RoomId == room.RoomId)).ShouldBeFalse();
        (await verifyDb.PowerPoints.AnyAsync(p => p.PowerPointId == powerPoint.PowerPointId)).ShouldBeFalse();
        (await verifyDb.Devices.AnyAsync(d => d.DeviceId == device.DeviceId)).ShouldBeFalse();
        (await verifyDb.ImportJobs.AnyAsync(j => j.ImportJobId == importJob.ImportJobId)).ShouldBeFalse();
        (await verifyDb.SmartPlugDailyData.AnyAsync(d => d.Id == dailyData.Id)).ShouldBeFalse();
        (await verifyDb.SmartPlugIntervalData.AnyAsync(d => d.Id == intervalData.Id)).ShouldBeFalse();
        (await verifyDb.InsightRuns.AnyAsync(r => r.RunId == insightRun.RunId)).ShouldBeFalse();
        (await verifyDb.Insights.AnyAsync(i => i.InsightId == insight.InsightId)).ShouldBeFalse();

        // Untouched flat and its child row must survive the cascade unharmed.
        (await verifyDb.Flats.AnyAsync(f => f.FlatId == otherFlat.FlatId)).ShouldBeTrue();
        (await verifyDb.MeterReadings.AnyAsync(r => r.ReadingId == otherMeterReading.ReadingId)).ShouldBeTrue();
    }
}
