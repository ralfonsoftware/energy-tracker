using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Shouldly;

namespace api.Tests.Shared;

public class TariffResolutionTests
{
    private static Tariff MakeTariff(DateTimeOffset contractStartDate, decimal pricePerKwh = 0.30m) =>
        new()
        {
            TariffId = Guid.NewGuid(),
            FlatId = Guid.NewGuid(),
            ContractStartDate = contractStartDate,
            PricePerKwh = pricePerKwh,
            MonthlyBaseFee = 10m
        };

    [Fact]
    public void Resolve_NullTariffs_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => TariffResolution.Resolve(null!, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Resolve_NoTariffs_ReturnsNull()
    {
        var result = TariffResolution.Resolve([], DateTimeOffset.UtcNow);

        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_TariffStartsExactlyOnDate_ReturnsIt()
    {
        var date = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var tariff = MakeTariff(date);

        var result = TariffResolution.Resolve([tariff], date);

        result.ShouldBe(tariff);
    }

    [Fact]
    public void Resolve_NoTariffActiveOnDate_ReturnsNull()
    {
        var tariff = MakeTariff(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
        var date = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

        var result = TariffResolution.Resolve([tariff], date);

        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_SingleActiveTariff_ReturnsIt()
    {
        var tariff = MakeTariff(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var date = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var result = TariffResolution.Resolve([tariff], date);

        result.ShouldBe(tariff);
    }

    [Fact]
    public void Resolve_MultipleTariffs_ReturnsMostRecentStartingOnOrBeforeDate()
    {
        var older = MakeTariff(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), 0.25m);
        var target = MakeTariff(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), 0.30m);
        var future = MakeTariff(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero), 0.35m);
        var date = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var result = TariffResolution.Resolve([older, target, future], date);

        result.ShouldBe(target);
    }

    [Fact]
    public void Resolve_TieOnContractStartDate_ResolvesDeterministicallyByHigherTariffId()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var lowerId = new Tariff
        {
            TariffId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            FlatId = Guid.NewGuid(),
            ContractStartDate = start,
            PricePerKwh = 0.25m,
            MonthlyBaseFee = 10m
        };
        var higherId = new Tariff
        {
            TariffId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            FlatId = Guid.NewGuid(),
            ContractStartDate = start,
            PricePerKwh = 0.30m,
            MonthlyBaseFee = 10m
        };
        var date = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var resultAscending = TariffResolution.Resolve([lowerId, higherId], date);
        var resultDescending = TariffResolution.Resolve([higherId, lowerId], date);

        resultAscending.ShouldBe(higherId);
        resultDescending.ShouldBe(higherId);
    }
}
