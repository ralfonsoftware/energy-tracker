using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Dashboard;
using Shouldly;

namespace api.Tests.Features.Dashboard;

public class KpiCalculatorTests
{
    private readonly KpiCalculator _calculator = new();
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);

    private static Flat MakeFlat(decimal baseline = 3650m) =>
        new() { FlatId = Guid.NewGuid(), UserId = "u", Name = "F", AnnualKwhBaseline = baseline, SpikeThreshold = 2.0m };

    private static MeterReading MakeReading(DateTimeOffset date, decimal kwh) =>
        new() { ReadingId = Guid.NewGuid(), FlatId = Guid.NewGuid(), ReadingDate = date, KwhValue = kwh, IsCorrected = false };

    private static Tariff MakeTariff(DateTimeOffset effectiveDate, decimal pricePerKwh) =>
        new() { TariffId = Guid.NewGuid(), FlatId = Guid.NewGuid(), EffectiveDate = effectiveDate, PricePerKwh = pricePerKwh, MonthlyBaseFee = 10m };

    [Fact]
    public void Compute_NoReadings_ReturnsAllZerosAndNullLastReadingDate()
    {
        var flat = MakeFlat();

        var result = _calculator.Compute(flat, [], [], Now);

        result.DailyAvgKwh.ShouldBe(0m);
        result.WeeklyAvgKwh.ShouldBe(0m);
        result.DailyAvgCost.ShouldBe(0m);
        result.WeeklyAvgCost.ShouldBe(0m);
        result.ProjectedMonthlyCost.ShouldBe(0m);
        result.LastReadingDate.ShouldBeNull();
        result.TodayKwh.ShouldBe(0m);
        result.SpikeDays.ShouldBeEmpty();
    }

    [Fact]
    public void Compute_OneReading_ReturnsZeroKpisWithLastReadingDateSet()
    {
        var flat = MakeFlat();
        var readingDate = new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero);
        var readings = new List<MeterReading> { MakeReading(readingDate, 100m) };

        var result = _calculator.Compute(flat, readings, [], Now);

        result.DailyAvgKwh.ShouldBe(0m);
        result.WeeklyAvgKwh.ShouldBe(0m);
        result.DailyAvgCost.ShouldBe(0m);
        result.ProjectedMonthlyCost.ShouldBe(0m);
        result.LastReadingDate.ShouldBe(readingDate);
    }

    [Fact]
    public void Compute_TwoReadings_ComputesDailyAvgKwhCorrectly()
    {
        var flat = MakeFlat();
        var date1 = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var date2 = date1.AddDays(10);
        var readings = new List<MeterReading>
        {
            MakeReading(date1, 100m),
            MakeReading(date2, 150m)
        };

        var result = _calculator.Compute(flat, readings, [], Now);

        result.DailyAvgKwh.ShouldBe(5m);
        result.WeeklyAvgKwh.ShouldBe(35m);
    }

    [Fact]
    public void Compute_TwoReadings_WithTariff_ComputesDailyAvgCostCorrectly()
    {
        var flat = MakeFlat();
        var date1 = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var date2 = date1.AddDays(10);
        var readings = new List<MeterReading>
        {
            MakeReading(date1, 100m),
            MakeReading(date2, 150m)
        };
        var tariffs = new List<Tariff> { MakeTariff(date1.AddDays(-1), 0.30m) };

        var result = _calculator.Compute(flat, readings, tariffs, Now);

        // totalCost = 50 kWh × 0.30 = 15m; dailyAvgCost = 15m / 10d = 1.5m
        result.DailyAvgCost.ShouldBe(1.5m);
    }

    [Fact]
    public void Compute_TwoReadings_NoTariff_CostFieldsAreZero()
    {
        var flat = MakeFlat();
        var date1 = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var date2 = date1.AddDays(10);
        var readings = new List<MeterReading>
        {
            MakeReading(date1, 100m),
            MakeReading(date2, 150m)
        };

        var result = _calculator.Compute(flat, readings, [], Now);

        result.DailyAvgCost.ShouldBe(0m);
        result.ProjectedMonthlyCost.ShouldBe(0m);
    }

    [Fact]
    public void Compute_MultipleReadings_TariffChange_UsesPeriodAccurateCost()
    {
        var flat = MakeFlat();
        var dateA = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var dateB = dateA.AddDays(10);
        var dateC = dateB.AddDays(10);
        var readings = new List<MeterReading>
        {
            MakeReading(dateA, 100m),
            MakeReading(dateB, 150m),
            MakeReading(dateC, 200m)
        };
        // Tariff T1 active at dateA (0.20€), T2 active at dateB (0.30€)
        var tariffs = new List<Tariff>
        {
            MakeTariff(dateA, 0.20m),
            MakeTariff(dateB, 0.30m)
        };

        var result = _calculator.Compute(flat, readings, tariffs, Now);

        // A→B: 50 kWh × 0.20 = 10m; B→C: 50 kWh × 0.30 = 15m → totalCost = 25m over 20 days
        result.DailyAvgCost.ShouldBe(1.25m);
    }

    [Fact]
    public void Compute_DailyBudgetKwh_IsAnnualBaselineDividedBy365()
    {
        var flat = MakeFlat(baseline: 3650m);

        var result = _calculator.Compute(flat, [], [], Now);

        result.DailyBudgetKwh.ShouldBe(10m);
    }

    [Fact]
    public void Compute_TodayKwh_IsLastIntervalDailyRate()
    {
        var flat = MakeFlat();
        var dateA = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var dateB = dateA.AddDays(10);
        var dateC = dateB.AddDays(5);
        var readings = new List<MeterReading>
        {
            MakeReading(dateA, 100m),
            MakeReading(dateB, 150m),
            MakeReading(dateC, 180m)
        };

        var result = _calculator.Compute(flat, readings, [], Now);

        // Last interval: B→C = 30 kWh over 5 days → TodayKwh = 6m
        result.TodayKwh.ShouldBe(6m);
    }

    [Fact]
    public void Compute_ProjectedMonthlyCost_UsesCurrentTariffPriceAndHistoricalKwhRate()
    {
        var flat = MakeFlat();
        var date1 = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var date2 = date1.AddDays(10);
        var readings = new List<MeterReading>
        {
            MakeReading(date1, 100m),
            MakeReading(date2, 150m)
        };
        // Current tariff (effective before Now)
        var tariffs = new List<Tariff> { MakeTariff(date1.AddDays(-30), 0.40m) };

        var result = _calculator.Compute(flat, readings, tariffs, Now);

        // DailyAvgKwh = 5m; current tariff = 0.40€; projected = 5m × 0.40m × 30m = 60m
        result.ProjectedMonthlyCost.ShouldBe(60m);
    }
}
