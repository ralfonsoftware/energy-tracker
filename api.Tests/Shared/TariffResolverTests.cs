using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api.Tests.Shared;

public class TariffResolverTests
{
    private static AppDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<(Guid flatId, AppDbContext db)> SeedAsync(params (DateTimeOffset date, decimal price)[] tariffs)
    {
        var db = MakeDb();
        var flatId = Guid.NewGuid();
        foreach (var (date, price) in tariffs)
        {
            db.Tariffs.Add(new Tariff
            {
                TariffId = Guid.NewGuid(),
                FlatId = flatId,
                EffectiveDate = date,
                PricePerKwh = price,
                MonthlyBaseFee = 10m
            });
        }
        await db.SaveChangesAsync();
        return (flatId, db);
    }

    [Fact]
    public async Task ResolveAsync_NoTariffs_ReturnsNull()
    {
        var db = MakeDb();
        var resolver = new TariffResolver(db);

        var result = await resolver.ResolveAsync(Guid.NewGuid(), DateTimeOffset.UtcNow, CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_DateBeforeAllTariffs_ReturnsNull()
    {
        var t1Date = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var (flatId, db) = await SeedAsync((t1Date, 0.30m));
        var resolver = new TariffResolver(db);

        var result = await resolver.ResolveAsync(flatId, t1Date.AddDays(-1), CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_DateOnEffectiveDate_ReturnsTariff()
    {
        var t1Date = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var (flatId, db) = await SeedAsync((t1Date, 0.30m));
        var resolver = new TariffResolver(db);

        var result = await resolver.ResolveAsync(flatId, t1Date, CancellationToken.None);

        result.ShouldNotBeNull();
        result!.PricePerKwh.ShouldBe(0.30m);
    }

    [Fact]
    public async Task ResolveAsync_DateBetweenTariffs_ReturnsEarlierOne()
    {
        var t1Date = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t2Date = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var (flatId, db) = await SeedAsync((t1Date, 0.25m), (t2Date, 0.35m));
        var resolver = new TariffResolver(db);

        var betweenDate = new DateTimeOffset(2025, 3, 15, 0, 0, 0, TimeSpan.Zero);
        var result = await resolver.ResolveAsync(flatId, betweenDate, CancellationToken.None);

        result.ShouldNotBeNull();
        result!.PricePerKwh.ShouldBe(0.25m);
    }

    [Fact]
    public async Task ResolveAsync_DateAfterAllTariffs_ReturnsMostRecent()
    {
        var t1Date = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t2Date = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t3Date = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var (flatId, db) = await SeedAsync((t1Date, 0.20m), (t2Date, 0.28m), (t3Date, 0.35m));
        var resolver = new TariffResolver(db);

        var result = await resolver.ResolveAsync(flatId, DateTimeOffset.UtcNow, CancellationToken.None);

        result.ShouldNotBeNull();
        result!.PricePerKwh.ShouldBe(0.35m);
    }
}
