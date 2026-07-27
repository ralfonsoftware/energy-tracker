using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api.Tests.Shared;

public class InsightDeduplicationTests
{
    private static AppDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task SeedInsightAsync(
        AppDbContext db, Guid flatId, InsightType type, Guid? deviceId, string data, DateTimeOffset? createdAt = null)
    {
        db.Insights.Add(new Insight
        {
            InsightId = Guid.NewGuid(),
            FlatId = flatId,
            Type = type,
            DeviceId = deviceId,
            Data = data,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task IsNearDuplicateOfMostRecentAsync_NoPriorRow_ReturnsFalse()
    {
        var db = MakeDb();

        var result = await InsightDeduplication.IsNearDuplicateOfMostRecentAsync(
            db, Guid.NewGuid(), InsightType.Standby, Guid.NewGuid(), 100m, CancellationToken.None);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsNearDuplicateOfMostRecentAsync_WithinFivePercentTolerance_ReturnsTrue()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        await SeedInsightAsync(db, flatId, InsightType.Standby, deviceId, """{"estimatedMonthlyCost":100.00}""");

        // 104 vs 100: diff 4, reference 104, tolerance 5.2 -> within tolerance.
        var result = await InsightDeduplication.IsNearDuplicateOfMostRecentAsync(
            db, flatId, InsightType.Standby, deviceId, 104m, CancellationToken.None);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsNearDuplicateOfMostRecentAsync_BeyondFivePercentTolerance_ReturnsFalse()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        await SeedInsightAsync(db, flatId, InsightType.Standby, deviceId, """{"estimatedMonthlyCost":100.00}""");

        // 110 vs 100: diff 10, reference 110, tolerance 5.5 -> beyond tolerance.
        var result = await InsightDeduplication.IsNearDuplicateOfMostRecentAsync(
            db, flatId, InsightType.Standby, deviceId, 110m, CancellationToken.None);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsNearDuplicateOfMostRecentAsync_BothValuesZero_ReturnsTrue()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        await SeedInsightAsync(db, flatId, InsightType.Budget, null, """{"overspendEur":0}""");

        var result = await InsightDeduplication.IsNearDuplicateOfMostRecentAsync(
            db, flatId, InsightType.Budget, null, 0m, CancellationToken.None);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsNearDuplicateOfMostRecentAsync_OneValueZeroOtherNonzero_ReturnsFalse()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        await SeedInsightAsync(db, flatId, InsightType.Budget, null, """{"overspendEur":0}""");

        var result = await InsightDeduplication.IsNearDuplicateOfMostRecentAsync(
            db, flatId, InsightType.Budget, null, 50m, CancellationToken.None);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsNearDuplicateOfMostRecentAsync_PriorRowMissingExpectedProperty_ReturnsFalse()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        await SeedInsightAsync(db, flatId, InsightType.Standby, deviceId, """{"someOtherField":123}""");

        var result = await InsightDeduplication.IsNearDuplicateOfMostRecentAsync(
            db, flatId, InsightType.Standby, deviceId, 100m, CancellationToken.None);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsNearDuplicateOfMostRecentAsync_PriorRowPropertyIsNonNumeric_ReturnsFalse()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        await SeedInsightAsync(db, flatId, InsightType.Standby, deviceId, """{"estimatedMonthlyCost":"not-a-number"}""");

        await Should.NotThrowAsync(() => InsightDeduplication.IsNearDuplicateOfMostRecentAsync(
            db, flatId, InsightType.Standby, deviceId, 100m, CancellationToken.None));

        var result = await InsightDeduplication.IsNearDuplicateOfMostRecentAsync(
            db, flatId, InsightType.Standby, deviceId, 100m, CancellationToken.None);
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsNearDuplicateOfMostRecentAsync_DifferentDeviceIds_AreIndependentIdentities()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var deviceA = Guid.NewGuid();
        var deviceB = Guid.NewGuid();
        await SeedInsightAsync(db, flatId, InsightType.Standby, deviceA, """{"estimatedMonthlyCost":100.00}""");

        // A near-duplicate value for device A must not suppress a write for device B, which has
        // no prior row of its own.
        var result = await InsightDeduplication.IsNearDuplicateOfMostRecentAsync(
            db, flatId, InsightType.Standby, deviceB, 101m, CancellationToken.None);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsNearDuplicateOfMostRecentAsync_MultiplePriorRows_ComparesAgainstMostRecentOnly()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await SeedInsightAsync(db, flatId, InsightType.Standby, deviceId, """{"estimatedMonthlyCost":50.00}""", now.AddDays(-2));
        await SeedInsightAsync(db, flatId, InsightType.Standby, deviceId, """{"estimatedMonthlyCost":100.00}""", now.AddDays(-1));

        // 104 is within tolerance of the most recent row's 100, but not the older row's 50 -
        // confirms the query picks the most recent row by CreatedAt, not just any match.
        var result = await InsightDeduplication.IsNearDuplicateOfMostRecentAsync(
            db, flatId, InsightType.Standby, deviceId, 104m, CancellationToken.None);

        result.ShouldBeTrue();
    }
}
