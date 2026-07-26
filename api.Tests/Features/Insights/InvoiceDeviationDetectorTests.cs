using System.Text.Json;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Insights;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api.Tests.Features.Insights;

public class InvoiceDeviationDetectorTests
{
    private static AppDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<Flat> SeedFlatAsync(AppDbContext db, decimal annualKwhBaseline = 3650m)
    {
        var flat = new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = "user-test-123",
            Name = "Test Flat",
            AnnualKwhBaseline = annualKwhBaseline,
            SpikeThreshold = 2.0m
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
    public async Task DetectAsync_ConsumptionFifteenPercentAboveBaseline_WritesInsightWithDirectionAbove()
    {
        var db = MakeDb();
        var now = DateTimeOffset.UtcNow;
        // baseline 3650 kWh/yr; 690 kWh over the 60-day window => dailyAvg 11.5 => projected 4197.5
        // (15% above baseline)
        var flat = await SeedFlatAsync(db, annualKwhBaseline: 3650m);
        await SeedReadingAsync(db, flat.FlatId, now.AddDays(-60), 1000m);
        await SeedReadingAsync(db, flat.FlatId, now, 1690m);
        await SeedTariffAsync(db, flat.FlatId, pricePerKwh: 0.30m, contractStartDate: now.AddYears(-1));
        var runId = Guid.NewGuid();

        await new InvoiceDeviationDetector(db).DetectAsync(flat.FlatId, runId, CancellationToken.None);

        var insight = await db.Insights.SingleAsync();
        insight.Type.ShouldBe(InsightType.InvoiceDeviation);
        insight.DeviceId.ShouldBeNull();
        insight.FlatId.ShouldBe(flat.FlatId);
        insight.RunId.ShouldBe(runId);

        using var json = JsonDocument.Parse(insight.Data);
        json.RootElement.GetProperty("projectedAnnualKwh").GetDecimal().ShouldBe(4197.5m);
        json.RootElement.GetProperty("baselineKwh").GetDecimal().ShouldBe(3650m);
        json.RootElement.GetProperty("deviationPct").GetDecimal().ShouldBe(15.0m);
        json.RootElement.GetProperty("impliedDeltaEur").GetDecimal().ShouldBe(164.25m);
        json.RootElement.GetProperty("direction").GetString().ShouldBe("above");
    }

    [Fact]
    public async Task DetectAsync_ConsumptionTwelvePercentBelowBaseline_WritesInsightWithDirectionBelow()
    {
        var db = MakeDb();
        var now = DateTimeOffset.UtcNow;
        // baseline 3650 kWh/yr; 528 kWh over the 60-day window => dailyAvg 8.8 => projected 3212
        // (12% below baseline)
        var flat = await SeedFlatAsync(db, annualKwhBaseline: 3650m);
        await SeedReadingAsync(db, flat.FlatId, now.AddDays(-60), 1000m);
        await SeedReadingAsync(db, flat.FlatId, now, 1528m);
        await SeedTariffAsync(db, flat.FlatId, pricePerKwh: 0.30m, contractStartDate: now.AddYears(-1));

        await new InvoiceDeviationDetector(db).DetectAsync(flat.FlatId, Guid.NewGuid(), CancellationToken.None);

        var insight = await db.Insights.SingleAsync();
        using var json = JsonDocument.Parse(insight.Data);
        json.RootElement.GetProperty("projectedAnnualKwh").GetDecimal().ShouldBe(3212m);
        json.RootElement.GetProperty("deviationPct").GetDecimal().ShouldBe(12.0m);
        json.RootElement.GetProperty("impliedDeltaEur").GetDecimal().ShouldBe(-131.4m);
        json.RootElement.GetProperty("direction").GetString().ShouldBe("below");
    }

    [Fact]
    public async Task DetectAsync_FlatNotFound_SkipsWithNoInsight()
    {
        var db = MakeDb();

        await Should.NotThrowAsync(() => new InvoiceDeviationDetector(db).DetectAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));

        (await db.Insights.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DetectAsync_DeviationExactlyTenPercentThreshold_WritesInsight()
    {
        var db = MakeDb();
        var now = DateTimeOffset.UtcNow;
        // baseline 3650 kWh/yr; 660 kWh over the 60-day window => dailyAvg 11.0 => projected 4015
        // (exactly 10% above baseline) -> strict `<` means this boundary still triggers an insight.
        var flat = await SeedFlatAsync(db, annualKwhBaseline: 3650m);
        await SeedReadingAsync(db, flat.FlatId, now.AddDays(-60), 1000m);
        await SeedReadingAsync(db, flat.FlatId, now, 1660m);
        await SeedTariffAsync(db, flat.FlatId, pricePerKwh: 0.30m, contractStartDate: now.AddYears(-1));

        await new InvoiceDeviationDetector(db).DetectAsync(flat.FlatId, Guid.NewGuid(), CancellationToken.None);

        var insight = await db.Insights.SingleAsync();
        using var json = JsonDocument.Parse(insight.Data);
        json.RootElement.GetProperty("projectedAnnualKwh").GetDecimal().ShouldBe(4015m);
        json.RootElement.GetProperty("deviationPct").GetDecimal().ShouldBe(10.0m);
        json.RootElement.GetProperty("direction").GetString().ShouldBe("above");
    }

    [Fact]
    public async Task DetectAsync_DeviationBelowTenPercentThreshold_WritesNoInsight()
    {
        var db = MakeDb();
        var now = DateTimeOffset.UtcNow;
        // baseline 3650 kWh/yr; projected 3942 kWh = 8% above baseline, below the +/-10% threshold.
        var flat = await SeedFlatAsync(db, annualKwhBaseline: 3650m);
        await SeedReadingAsync(db, flat.FlatId, now.AddDays(-60), 1000m);
        await SeedReadingAsync(db, flat.FlatId, now, 1648m);
        await SeedTariffAsync(db, flat.FlatId, pricePerKwh: 0.30m, contractStartDate: now.AddYears(-1));

        await new InvoiceDeviationDetector(db).DetectAsync(flat.FlatId, Guid.NewGuid(), CancellationToken.None);

        (await db.Insights.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DetectAsync_FewerThanSixtyDaysWindowCoverage_SkipsWithNoInsight()
    {
        var db = MakeDb();
        var now = DateTimeOffset.UtcNow;
        var flat = await SeedFlatAsync(db, annualKwhBaseline: 3650m);
        // No reading exists at or before now - 60 days -> anchor cannot be resolved.
        await SeedReadingAsync(db, flat.FlatId, now.AddDays(-10), 1000m);
        await SeedReadingAsync(db, flat.FlatId, now, 2000m);
        await SeedTariffAsync(db, flat.FlatId, pricePerKwh: 0.30m, contractStartDate: now.AddYears(-1));

        await new InvoiceDeviationDetector(db).DetectAsync(flat.FlatId, Guid.NewGuid(), CancellationToken.None);

        (await db.Insights.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DetectAsync_WindowValidButNoTariffResolvesForNow_SkipsWithNoInsight()
    {
        var db = MakeDb();
        var now = DateTimeOffset.UtcNow;
        // Same 15%-above-baseline window as the first test, but no tariff seeded at all.
        var flat = await SeedFlatAsync(db, annualKwhBaseline: 3650m);
        await SeedReadingAsync(db, flat.FlatId, now.AddDays(-60), 1000m);
        await SeedReadingAsync(db, flat.FlatId, now, 1690m);

        await Should.NotThrowAsync(() => new InvoiceDeviationDetector(db).DetectAsync(flat.FlatId, Guid.NewGuid(), CancellationToken.None));

        (await db.Insights.CountAsync()).ShouldBe(0);
    }
}
