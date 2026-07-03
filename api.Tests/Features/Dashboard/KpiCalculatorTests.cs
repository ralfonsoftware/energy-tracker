using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Dashboard;
using Shouldly;

namespace api.Tests.Features.Dashboard;

public class KpiCalculatorTests
{
    private readonly KpiCalculator _calculator = new();
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);

    private static Flat MakeFlat(decimal baseline = 3650m, decimal spikeThreshold = 2.0m) =>
        new() { FlatId = Guid.NewGuid(), UserId = "u", Name = "F", AnnualKwhBaseline = baseline, SpikeThreshold = spikeThreshold };

    private static MeterReading MakeReading(DateTimeOffset date, decimal kwh) =>
        new() { ReadingId = Guid.NewGuid(), FlatId = Guid.NewGuid(), ReadingDate = date, KwhValue = kwh, IsCorrected = false };

    private static Tariff MakeTariff(DateTimeOffset contractStartDate, decimal pricePerKwh) =>
        new() { TariffId = Guid.NewGuid(), FlatId = Guid.NewGuid(), ContractStartDate = contractStartDate, PricePerKwh = pricePerKwh, MonthlyBaseFee = 10m };

    [Fact]
    public void Compute_NoReadings_ReturnsAllZerosAndNullLastReadingDate()
    {
        var flat = MakeFlat();

        var result = _calculator.Compute(flat, [], [], Now);

        result.DailyAvgKwh.ShouldBe(0m);
        result.WeeklyAvgKwh.ShouldBe(0m);
        result.LastReadingDate.ShouldBeNull();
        result.TodayKwh.ShouldBe(0m);
        result.SpikeDays.ShouldBeEmpty();
        result.Cost.ShouldBeNull();
        result.LastKwhValue.ShouldBeNull();
        result.DailyConsumption.ShouldBeEmpty();
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
        result.LastReadingDate.ShouldBe(readingDate);
        result.Cost.ShouldBeNull();
        result.LastKwhValue.ShouldBe(100m);
        result.DailyConsumption.ShouldBeEmpty();
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
        result.LastKwhValue.ShouldBe(150m);
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
        result.Cost.ShouldNotBeNull();
        result.Cost!.DailyAvgCost.ShouldBe(1.5m);
    }

    [Fact]
    public void Compute_NoTariffs_CostIsNull()
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

        result.Cost.ShouldBeNull();
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
        result.Cost.ShouldNotBeNull();
        result.Cost!.DailyAvgCost.ShouldBe(1.25m);
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
        result.Cost.ShouldNotBeNull();
        result.Cost!.ProjectedMonthlyCost.ShouldBe(60m);
    }

    [Fact]
    public void Compute_AllIntervalsCovered_HasCostGapFalseAndFullDenominator()
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

        result.Cost.ShouldNotBeNull();
        result.Cost!.DailyAvgCost.ShouldBe(1.5m);
        result.Cost!.HasCostGap.ShouldBeFalse();
        result.Cost!.CoveredDays.ShouldBe(10);
        result.Cost!.TotalDays.ShouldBe(10);
    }

    [Fact]
    public void Compute_FirstIntervalUntariffed_DividedByCoveredDaysOnlyAndHasCostGapTrue()
    {
        var flat = MakeFlat();
        var dateA = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var dateB = dateA.AddDays(10);
        var dateC = dateB.AddDays(10);
        var readings = new List<MeterReading>
        {
            MakeReading(dateA, 100m),
            MakeReading(dateB, 150m),
            MakeReading(dateC, 200m)
        };
        // Tariff effective at dateB only — interval A→B uncovered, B→C covered
        var tariffs = new List<Tariff> { MakeTariff(dateB, 0.30m) };

        var result = _calculator.Compute(flat, readings, tariffs, Now);

        result.Cost.ShouldNotBeNull();
        // Not 0.75m (25m total-span average) — regression anchor for the coveredDays-only denominator
        result.Cost!.DailyAvgCost.ShouldBe(1.5m);
        result.Cost!.HasCostGap.ShouldBeTrue();
        result.Cost!.CoveredDays.ShouldBe(10);
        result.Cost!.TotalDays.ShouldBe(20);
    }

    [Fact]
    public void Compute_CoveredDaysLessThanMinCostDetailDays_CostDetailAvailableFalse()
    {
        var flat = MakeFlat();
        var date1 = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var date2 = date1.AddDays(3);
        var readings = new List<MeterReading>
        {
            MakeReading(date1, 100m),
            MakeReading(date2, 110m)
        };
        var tariffs = new List<Tariff> { MakeTariff(date1.AddDays(-1), 0.30m) };

        var result = _calculator.Compute(flat, readings, tariffs, Now);

        result.Cost.ShouldNotBeNull();
        result.Cost!.CoveredDays.ShouldBe(3);
        result.Cost!.CostDetailAvailable.ShouldBeFalse();
    }

    [Fact]
    public void Compute_CoveredDaysExactlyEqualsMinCostDetailDays_CostDetailAvailableTrue()
    {
        var flat = MakeFlat();
        var date1 = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var date2 = date1.AddDays(7);
        var readings = new List<MeterReading>
        {
            MakeReading(date1, 100m),
            MakeReading(date2, 114m)
        };
        var tariffs = new List<Tariff> { MakeTariff(date1.AddDays(-1), 0.30m) };

        var result = _calculator.Compute(flat, readings, tariffs, Now);

        // Boundary anchor: CoveredDays == MinCostDetailDays (7) must be CostDetailAvailable=true (>=, not >)
        result.Cost.ShouldNotBeNull();
        result.Cost!.CoveredDays.ShouldBe(7);
        result.Cost!.CostDetailAvailable.ShouldBeTrue();
    }

    [Fact]
    public void Compute_MultipleReadings_DailyConsumption_HasSevenEntriesEndingAtNowDate()
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

        result.DailyConsumption.Length.ShouldBe(7);
        result.DailyConsumption[0].Date.ShouldBe("2026-06-24");
        result.DailyConsumption[^1].Date.ShouldBe("2026-06-30");
    }

    [Fact]
    public void Compute_TwoReadingsWithinWindow_DistributesKwhEvenlyAcrossSpannedDays()
    {
        var flat = MakeFlat();
        var date1 = new DateTimeOffset(2026, 6, 27, 0, 0, 0, TimeSpan.Zero);
        var date2 = new DateTimeOffset(2026, 6, 29, 0, 0, 0, TimeSpan.Zero);
        var readings = new List<MeterReading>
        {
            MakeReading(date1, 100m),
            MakeReading(date2, 106m)
        };

        var result = _calculator.Compute(flat, readings, [], Now);

        result.DailyConsumption.First(d => d.Date == "2026-06-27").KwhValue.ShouldBe(0m);
        result.DailyConsumption.First(d => d.Date == "2026-06-28").KwhValue.ShouldBe(3m);
        result.DailyConsumption.First(d => d.Date == "2026-06-29").KwhValue.ShouldBe(3m);
    }

    [Fact]
    public void Compute_DaySpikeAboveThreshold_IsFlaggedInSpikeDays()
    {
        var flat = MakeFlat();
        var readings = new List<MeterReading>
        {
            MakeReading(new DateTimeOffset(2026, 6, 22, 0, 0, 0, TimeSpan.Zero), 100m),
            MakeReading(new DateTimeOffset(2026, 6, 23, 0, 0, 0, TimeSpan.Zero), 101m),
            MakeReading(new DateTimeOffset(2026, 6, 24, 0, 0, 0, TimeSpan.Zero), 102m),
            MakeReading(new DateTimeOffset(2026, 6, 25, 0, 0, 0, TimeSpan.Zero), 103m),
            MakeReading(new DateTimeOffset(2026, 6, 26, 0, 0, 0, TimeSpan.Zero), 104m),
            MakeReading(new DateTimeOffset(2026, 6, 27, 0, 0, 0, TimeSpan.Zero), 105m),
            MakeReading(new DateTimeOffset(2026, 6, 28, 0, 0, 0, TimeSpan.Zero), 106m),
            MakeReading(new DateTimeOffset(2026, 6, 29, 0, 0, 0, TimeSpan.Zero), 107m),
            MakeReading(new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero), 110m)
        };

        var result = _calculator.Compute(flat, readings, [], Now);

        result.SpikeDays.ShouldContain("2026-06-30");
    }

    [Fact]
    public void Compute_DayExactlyAtThreshold_IsNotFlaggedAsSpike()
    {
        var flat = MakeFlat();
        var readings = new List<MeterReading>
        {
            MakeReading(new DateTimeOffset(2026, 6, 22, 0, 0, 0, TimeSpan.Zero), 100m),
            MakeReading(new DateTimeOffset(2026, 6, 23, 0, 0, 0, TimeSpan.Zero), 101m),
            MakeReading(new DateTimeOffset(2026, 6, 24, 0, 0, 0, TimeSpan.Zero), 102m),
            MakeReading(new DateTimeOffset(2026, 6, 25, 0, 0, 0, TimeSpan.Zero), 103m),
            MakeReading(new DateTimeOffset(2026, 6, 26, 0, 0, 0, TimeSpan.Zero), 104m),
            MakeReading(new DateTimeOffset(2026, 6, 27, 0, 0, 0, TimeSpan.Zero), 105m),
            MakeReading(new DateTimeOffset(2026, 6, 28, 0, 0, 0, TimeSpan.Zero), 106m),
            MakeReading(new DateTimeOffset(2026, 6, 29, 0, 0, 0, TimeSpan.Zero), 107m),
            MakeReading(new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero), 109m)
        };

        var result = _calculator.Compute(flat, readings, [], Now);

        result.SpikeDays.ShouldNotContain("2026-06-30");
    }

    [Fact]
    public void Compute_CustomSpikeThreshold_UsesConfiguredValueNotDefault()
    {
        var flat = MakeFlat(spikeThreshold: 1.2m);
        var readings = new List<MeterReading>
        {
            MakeReading(new DateTimeOffset(2026, 6, 22, 0, 0, 0, TimeSpan.Zero), 100m),
            MakeReading(new DateTimeOffset(2026, 6, 23, 0, 0, 0, TimeSpan.Zero), 110m),
            MakeReading(new DateTimeOffset(2026, 6, 24, 0, 0, 0, TimeSpan.Zero), 120m),
            MakeReading(new DateTimeOffset(2026, 6, 25, 0, 0, 0, TimeSpan.Zero), 130m),
            MakeReading(new DateTimeOffset(2026, 6, 26, 0, 0, 0, TimeSpan.Zero), 140m),
            MakeReading(new DateTimeOffset(2026, 6, 27, 0, 0, 0, TimeSpan.Zero), 150m),
            MakeReading(new DateTimeOffset(2026, 6, 28, 0, 0, 0, TimeSpan.Zero), 160m),
            MakeReading(new DateTimeOffset(2026, 6, 29, 0, 0, 0, TimeSpan.Zero), 170m),
            MakeReading(new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero), 183m)
        };

        var result = _calculator.Compute(flat, readings, [], Now);

        result.SpikeDays.ShouldContain("2026-06-30");
    }

    [Fact]
    public void Compute_FirstDayInWindowNoPriorData_IsNotFlaggedAsSpike()
    {
        var flat = MakeFlat();
        var date1 = new DateTimeOffset(2026, 6, 29, 0, 0, 0, TimeSpan.Zero);
        var date2 = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var readings = new List<MeterReading>
        {
            MakeReading(date1, 100m),
            MakeReading(date2, 1100m)
        };

        var result = _calculator.Compute(flat, readings, [], Now);

        result.SpikeDays.ShouldNotContain("2026-06-30");
    }
}
