using System.Text.Json;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Insights;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api.Tests.Features.Insights;

public class BudgetAlertDetectorTests
{
    private static AppDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<Flat> SeedFlatAsync(AppDbContext db, decimal? plannedAnnualSpend, decimal annualKwhBaseline = 3650m)
    {
        var flat = new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = "user-test-123",
            Name = "Test Flat",
            AnnualKwhBaseline = annualKwhBaseline,
            SpikeThreshold = 2.0m,
            PlannedAnnualSpend = plannedAnnualSpend
        };
        db.Flats.Add(flat);
        await db.SaveChangesAsync();
        return flat;
    }

    private static async Task SeedReadingAsync(AppDbContext db, Guid flatId, DateTimeOffset readingDate, decimal kwhValue)
    {
        db.MeterReadings.Add(new MeterReading { ReadingId = Guid.NewGuid(), FlatId = flatId, ReadingDate = readingDate, KwhValue = kwhValue });
        await db.SaveChangesAsync();
    }

    private static async Task SeedTariffAsync(AppDbContext db, Guid flatId, decimal pricePerKwh, DateTimeOffset contractStartDate)
    {
        db.Tariffs.Add(new Tariff
        {
            FlatId = flatId,
            ContractStartDate = contractStartDate,
            PricePerKwh = pricePerKwh,
            MonthlyBaseFee = 0m
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task DetectAsync_ProjectedCostExceedsPlannedSpend_WritesInsightWithCorrectFigures()
    {
        var db = MakeDb();
        var now = DateTimeOffset.UtcNow;
        var flat = await SeedFlatAsync(db, plannedAnnualSpend: 800m);
        // 400 kWh over the 40-day window => dailyAverageCost = (400 * 0.30) / 40 = 3 => projected = 1095
        await SeedReadingAsync(db, flat.FlatId, now.AddDays(-40), 1000m);
        await SeedReadingAsync(db, flat.FlatId, now, 1400m);
        await SeedTariffAsync(db, flat.FlatId, pricePerKwh: 0.30m, contractStartDate: now.AddYears(-1));
        var runId = Guid.NewGuid();

        await new BudgetAlertDetector(db).DetectAsync(flat.FlatId, runId, CancellationToken.None);

        var insight = await db.Insights.SingleAsync();
        insight.Type.ShouldBe(InsightType.Budget);
        insight.DeviceId.ShouldBeNull();
        insight.FlatId.ShouldBe(flat.FlatId);
        insight.RunId.ShouldBe(runId);

        using var json = JsonDocument.Parse(insight.Data);
        json.RootElement.GetProperty("projectedAnnualCost").GetDecimal().ShouldBe(1095m);
        json.RootElement.GetProperty("plannedAnnualSpend").GetDecimal().ShouldBe(800m);
        json.RootElement.GetProperty("overspendEur").GetDecimal().ShouldBe(295m);
    }

    [Fact]
    public async Task DetectAsync_ProjectedCostWithinPlannedSpend_WritesNoInsight()
    {
        var db = MakeDb();
        var now = DateTimeOffset.UtcNow;
        var flat = await SeedFlatAsync(db, plannedAnnualSpend: 1200m); // > 1095 projected
        await SeedReadingAsync(db, flat.FlatId, now.AddDays(-40), 1000m);
        await SeedReadingAsync(db, flat.FlatId, now, 1400m);
        await SeedTariffAsync(db, flat.FlatId, pricePerKwh: 0.30m, contractStartDate: now.AddYears(-1));

        await new BudgetAlertDetector(db).DetectAsync(flat.FlatId, Guid.NewGuid(), CancellationToken.None);

        (await db.Insights.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DetectAsync_PlannedAnnualSpendNull_SkipsWithNoInsight()
    {
        var db = MakeDb();
        var now = DateTimeOffset.UtcNow;
        var flat = await SeedFlatAsync(db, plannedAnnualSpend: null);
        await SeedReadingAsync(db, flat.FlatId, now.AddDays(-40), 1000m);
        await SeedReadingAsync(db, flat.FlatId, now, 1400m);
        await SeedTariffAsync(db, flat.FlatId, pricePerKwh: 0.30m, contractStartDate: now.AddYears(-1));

        await Should.NotThrowAsync(() => new BudgetAlertDetector(db).DetectAsync(flat.FlatId, Guid.NewGuid(), CancellationToken.None));

        (await db.Insights.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DetectAsync_FewerThanThirtyDaysWindowCoverage_SkipsWithNoInsight()
    {
        var db = MakeDb();
        var now = DateTimeOffset.UtcNow;
        var flat = await SeedFlatAsync(db, plannedAnnualSpend: 500m);
        // No reading exists at or before now - 30 days -> anchor cannot be resolved.
        await SeedReadingAsync(db, flat.FlatId, now.AddDays(-10), 1000m);
        await SeedReadingAsync(db, flat.FlatId, now, 1400m);
        await SeedTariffAsync(db, flat.FlatId, pricePerKwh: 0.30m, contractStartDate: now.AddYears(-1));

        await new BudgetAlertDetector(db).DetectAsync(flat.FlatId, Guid.NewGuid(), CancellationToken.None);

        (await db.Insights.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DetectAsync_FlatNotFound_SkipsWithNoInsight()
    {
        var db = MakeDb();

        await Should.NotThrowAsync(() => new BudgetAlertDetector(db).DetectAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));

        (await db.Insights.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DetectAsync_PeriodPredatesEveryTariff_ExcludedFromCostSumWithoutThrowing()
    {
        var db = MakeDb();
        var now = DateTimeOffset.UtcNow;
        var flat = await SeedFlatAsync(db, plannedAnnualSpend: 500m);
        await SeedReadingAsync(db, flat.FlatId, now.AddDays(-40), 1000m);
        await SeedReadingAsync(db, flat.FlatId, now.AddDays(-20), 1200m);
        await SeedReadingAsync(db, flat.FlatId, now, 1400m);
        // Tariff starts after the first period's start date (now-40), so that period's cost is
        // excluded; the second period (starting now-20) is covered.
        await SeedTariffAsync(db, flat.FlatId, pricePerKwh: 0.30m, contractStartDate: now.AddDays(-25));

        await Should.NotThrowAsync(() => new BudgetAlertDetector(db).DetectAsync(flat.FlatId, Guid.NewGuid(), CancellationToken.None));

        // Only the second 200 kWh period is costed: (200 * 0.30) / 40 days * 365 = 547.5
        var insight = await db.Insights.SingleAsync();
        using var json = JsonDocument.Parse(insight.Data);
        json.RootElement.GetProperty("projectedAnnualCost").GetDecimal().ShouldBe(547.5m);
    }
}
