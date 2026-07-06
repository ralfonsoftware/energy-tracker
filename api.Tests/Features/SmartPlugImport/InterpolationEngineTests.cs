using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.SmartPlugImport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace api.Tests.Features.SmartPlugImport;

public class InterpolationEngineTests
{
    private static AppDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static InterpolationEngine MakeEngine(AppDbContext db) =>
        new(db, Mock.Of<ILogger<InterpolationEngine>>());

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
    public async Task InterpolateGapsAsync_SingleDayGap_FillsLinearlyInterpolatedValue()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        const string plugId = "plug-1";
        // A full flat 7-day history before the gap (avg = 4.0) so the cap never binds — this
        // test isolates the raw linear-interpolation math, not the 7-day cap (see the
        // dedicated SevenDayCap test for that).
        for (var i = 0; i < 7; i++)
            await SeedDailyRowAsync(db, flatId, plugId, new DateOnly(2025, 12, 26).AddDays(i), 4.0m);
        await SeedDailyRowAsync(db, flatId, plugId, new DateOnly(2026, 1, 3), 0.0m);

        var result = await MakeEngine(db).InterpolateGapsAsync(flatId, plugId, CancellationToken.None);

        var filled = await db.SmartPlugDailyData.SingleAsync(d => d.Date == new DateOnly(2026, 1, 2));
        filled.KwhValue.ShouldBe(2.0m);
        filled.IsInterpolated.ShouldBeTrue();
        result.Gaps.ShouldBe([new GapRange(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 2))]);
        result.PeriodStart.ShouldBe(new DateOnly(2025, 12, 26));
        result.PeriodEnd.ShouldBe(new DateOnly(2026, 1, 3));
    }

    [Fact]
    public async Task InterpolateGapsAsync_MultiDayGap_FillsEveryMissingDayProportionally()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        const string plugId = "plug-1";
        // Flat 7-day history before the gap (avg = 5.0) so the cap never binds.
        for (var i = 0; i < 7; i++)
            await SeedDailyRowAsync(db, flatId, plugId, new DateOnly(2026, 1, 1).AddDays(i), 5.0m);
        await SeedDailyRowAsync(db, flatId, plugId, new DateOnly(2026, 1, 11), 1.0m);

        await MakeEngine(db).InterpolateGapsAsync(flatId, plugId, CancellationToken.None);

        var day8 = await db.SmartPlugDailyData.SingleAsync(d => d.Date == new DateOnly(2026, 1, 8));
        var day9 = await db.SmartPlugDailyData.SingleAsync(d => d.Date == new DateOnly(2026, 1, 9));
        var day10 = await db.SmartPlugDailyData.SingleAsync(d => d.Date == new DateOnly(2026, 1, 10));

        day8.KwhValue.ShouldBe(4.0m);
        day9.KwhValue.ShouldBe(3.0m);
        day10.KwhValue.ShouldBe(2.0m);
        day8.IsInterpolated.ShouldBeTrue();
        day9.IsInterpolated.ShouldBeTrue();
        day10.IsInterpolated.ShouldBeTrue();
    }

    [Fact]
    public async Task InterpolateGapsAsync_SevenDayCap_ClampsToPreGapAverage()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        const string plugId = "plug-1";

        // 7 real days immediately before the gap, averaging 1.0
        for (var i = 0; i < 7; i++)
            await SeedDailyRowAsync(db, flatId, plugId, new DateOnly(2026, 1, 1).AddDays(i), 1.0m);

        // Anchor before the gap is 2026-01-07 = 1.0; anchor after is far higher, so raw
        // linear interpolation for the missing day would exceed the 7-day average cap (1.0).
        await SeedDailyRowAsync(db, flatId, plugId, new DateOnly(2026, 1, 9), 21.0m);

        await MakeEngine(db).InterpolateGapsAsync(flatId, plugId, CancellationToken.None);

        var filled = await db.SmartPlugDailyData.SingleAsync(d => d.Date == new DateOnly(2026, 1, 8));
        // Raw interpolation would be 1.0 + (21.0-1.0)*1/2 = 11.0, capped down to the 7-day average of 1.0.
        filled.KwhValue.ShouldBe(1.0m);
        filled.IsInterpolated.ShouldBeTrue();
    }

    [Fact]
    public async Task InterpolateGapsAsync_ZeroValueRow_NeverTreatedAsMissing()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        const string plugId = "plug-1";
        await SeedDailyRowAsync(db, flatId, plugId, new DateOnly(2026, 1, 1), 1.0m);
        await SeedDailyRowAsync(db, flatId, plugId, new DateOnly(2026, 1, 2), 0.0m);
        await SeedDailyRowAsync(db, flatId, plugId, new DateOnly(2026, 1, 3), 3.0m);

        var result = await MakeEngine(db).InterpolateGapsAsync(flatId, plugId, CancellationToken.None);

        result.Gaps.ShouldBeEmpty();
        var day2 = await db.SmartPlugDailyData.SingleAsync(d => d.Date == new DateOnly(2026, 1, 2));
        day2.KwhValue.ShouldBe(0.0m);
        day2.IsInterpolated.ShouldBeFalse();
        (await db.SmartPlugDailyData.CountAsync()).ShouldBe(3);
    }

    [Fact]
    public async Task InterpolateGapsAsync_DatesOutsideCoveredRange_NeverTouched()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        const string plugId = "plug-1";
        await SeedDailyRowAsync(db, flatId, plugId, new DateOnly(2026, 1, 5), 1.0m);
        await SeedDailyRowAsync(db, flatId, plugId, new DateOnly(2026, 1, 10), 2.0m);

        await MakeEngine(db).InterpolateGapsAsync(flatId, plugId, CancellationToken.None);

        var allDates = await db.SmartPlugDailyData.Select(d => d.Date).ToListAsync();
        allDates.ShouldNotContain(new DateOnly(2026, 1, 4));
        allDates.ShouldNotContain(new DateOnly(2026, 1, 11));
        allDates.ShouldAllBe(d => d >= new DateOnly(2026, 1, 5) && d <= new DateOnly(2026, 1, 10));
    }

    [Fact]
    public async Task InterpolateGapsAsync_NoExistingRows_ReturnsEmptyResultAndPerformsNoWrites()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();

        var result = await MakeEngine(db).InterpolateGapsAsync(flatId, "plug-none", CancellationToken.None);

        result.PeriodStart.ShouldBeNull();
        result.PeriodEnd.ShouldBeNull();
        result.Gaps.ShouldBeEmpty();
        (await db.SmartPlugDailyData.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task InterpolateGapsAsync_TwoIndependentGaps_EachGetsOwnRangeAndSeparateSevenDayAverage()
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        const string plugId = "plug-1";

        // First gap: 01-01 .. 01-03, missing 01-02.
        await SeedDailyRowAsync(db, flatId, plugId, new DateOnly(2026, 1, 1), 10.0m);
        await SeedDailyRowAsync(db, flatId, plugId, new DateOnly(2026, 1, 3), 10.0m);
        // Second gap: 01-05 .. 01-07, missing 01-06. Its own 7-day-preceding average must be
        // computed from real rows only (01-01 and 01-03..05), not from the first gap's
        // interpolated 01-02 row.
        await SeedDailyRowAsync(db, flatId, plugId, new DateOnly(2026, 1, 4), 2.0m);
        await SeedDailyRowAsync(db, flatId, plugId, new DateOnly(2026, 1, 5), 2.0m);
        await SeedDailyRowAsync(db, flatId, plugId, new DateOnly(2026, 1, 7), 2.0m);

        var result = await MakeEngine(db).InterpolateGapsAsync(flatId, plugId, CancellationToken.None);

        result.Gaps.ShouldBe([
            new GapRange(new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 2)),
            new GapRange(new DateOnly(2026, 1, 6), new DateOnly(2026, 1, 6))
        ]);
        var day6 = await db.SmartPlugDailyData.SingleAsync(d => d.Date == new DateOnly(2026, 1, 6));
        day6.KwhValue.ShouldBe(2.0m);
    }

    private sealed class ThrowOnSaveDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("Simulated concurrent insert conflict.");
    }

    [Fact]
    public async Task InterpolateGapsAsync_ConcurrentSaveConflict_DetachesNewRowsAndReturnsNoGaps()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var flatId = Guid.NewGuid();
        const string plugId = "plug-1";
        await SeedDailyRowAsync(new AppDbContext(options), flatId, plugId, new DateOnly(2026, 1, 1), 1.0m);
        await SeedDailyRowAsync(new AppDbContext(options), flatId, plugId, new DateOnly(2026, 1, 3), 3.0m);

        var db = new ThrowOnSaveDbContext(options);

        var result = await MakeEngine(db).InterpolateGapsAsync(flatId, plugId, CancellationToken.None);

        result.Gaps.ShouldBeEmpty();
        result.PeriodStart.ShouldBe(new DateOnly(2026, 1, 1));
        result.PeriodEnd.ShouldBe(new DateOnly(2026, 1, 3));
        // The newly-added rows must be detached, not left tracked in Added state — otherwise a
        // later SaveChangesAsync on this same (request-scoped) context would try to re-insert
        // them and fail again. The two pre-existing anchor rows loaded by the engine's own query
        // remain tracked as Unchanged, which is fine.
        db.ChangeTracker.Entries<SmartPlugDailyData>().ShouldAllBe(e => e.State == EntityState.Unchanged);
    }
}
