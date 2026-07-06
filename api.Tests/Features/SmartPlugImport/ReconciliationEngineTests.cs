using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.SmartPlugImport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace api.Tests.Features.SmartPlugImport;

public class ReconciliationEngineTests
{
    private static AppDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ReconciliationEngine MakeEngine(AppDbContext db) =>
        new(db, Mock.Of<ILogger<ReconciliationEngine>>());

    // Noon UTC keeps the local (Europe/Berlin) calendar date unambiguous regardless of DST.
    private static DateTimeOffset NoonUtc(int year, int month, int day) => new(year, month, day, 12, 0, 0, TimeSpan.Zero);

    private static async Task SeedReadingAsync(AppDbContext db, Guid flatId, DateTimeOffset date, decimal kwh)
    {
        db.MeterReadings.Add(new MeterReading { FlatId = flatId, ReadingDate = date, KwhValue = kwh });
        await db.SaveChangesAsync();
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

    [Fact]
    public async Task ReconcileAsync_CleanPeriodWithinTightTolerance_DoesNotThrow()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        await SeedReadingAsync(db, flatId, NoonUtc(2026, 1, 1), 0m);
        await SeedReadingAsync(db, flatId, NoonUtc(2026, 1, 11), 100m);
        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 5), 100m);

        await Should.NotThrowAsync(() =>
            MakeEngine(db).ReconcileAsync(flatId, new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 11), CancellationToken.None));
    }

    [Fact]
    public async Task ReconcileAsync_InterpolatedPeriodWithinWiderTolerance_DoesNotThrow()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        await SeedReadingAsync(db, flatId, NoonUtc(2026, 1, 1), 0m);
        await SeedReadingAsync(db, flatId, NoonUtc(2026, 1, 11), 100m);
        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 5), 90m);
        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 6), 10.7m, isInterpolated: true);

        await Should.NotThrowAsync(() =>
            MakeEngine(db).ReconcileAsync(flatId, new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 11), CancellationToken.None));
    }

    [Fact]
    public async Task ReconcileAsync_OverAttributionBeyondTolerance_Throws()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        await SeedReadingAsync(db, flatId, NoonUtc(2026, 1, 1), 0m);
        await SeedReadingAsync(db, flatId, NoonUtc(2026, 1, 11), 100m);
        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 5), 100.5m);

        await Should.ThrowAsync<OverAttributionException>(() =>
            MakeEngine(db).ReconcileAsync(flatId, new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 11), CancellationToken.None));
    }

    [Fact]
    public async Task ReconcileAsync_FewerThanTwoReadings_SkipsWithoutThrowing()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        await SeedReadingAsync(db, flatId, NoonUtc(2026, 1, 1), 0m);
        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 5), 100000m);

        await Should.NotThrowAsync(() =>
            MakeEngine(db).ReconcileAsync(flatId, new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 11), CancellationToken.None));
    }

    [Fact]
    public async Task ReconcileAsync_ReadingCoverageDoesNotFullySpanPeriod_SkipsWithoutThrowing()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        await SeedReadingAsync(db, flatId, NoonUtc(2026, 1, 1), 0m);
        await SeedReadingAsync(db, flatId, NoonUtc(2026, 1, 5), 10m);
        await SeedDailyRowAsync(db, flatId, "plug-1", new DateOnly(2026, 1, 5), 100000m);

        // Requested period extends to 01-11, but the latest reading only covers up to 01-05.
        await Should.NotThrowAsync(() =>
            MakeEngine(db).ReconcileAsync(flatId, new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 11), CancellationToken.None));
    }

    [Fact]
    public async Task ReconcileAsync_SumsAttributedKwhAcrossMultiplePlugIds()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        await SeedReadingAsync(db, flatId, NoonUtc(2026, 1, 1), 0m);
        await SeedReadingAsync(db, flatId, NoonUtc(2026, 1, 11), 100m);
        await SeedDailyRowAsync(db, flatId, "plug-a", new DateOnly(2026, 1, 5), 60m);
        await SeedDailyRowAsync(db, flatId, "plug-b", new DateOnly(2026, 1, 6), 40.5m);

        await Should.ThrowAsync<OverAttributionException>(() =>
            MakeEngine(db).ReconcileAsync(flatId, new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 11), CancellationToken.None));
    }
}
