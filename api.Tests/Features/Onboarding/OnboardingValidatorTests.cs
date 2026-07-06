using EnergyTracker.Api.Features.Onboarding;
using Shouldly;

namespace api.Tests.Features.Onboarding;

public class OnboardingValidatorTests
{
    private static CompleteOnboardingRequest MakeRequest(
        decimal annualKwhBaseline = 3500m, decimal pricePerKwh = 0.35m, decimal monthlyBaseFee = 10m,
        decimal? plannedAnnualSpend = null) =>
        new(
            FlatName: "Test Flat",
            AnnualKwhBaseline: annualKwhBaseline,
            PlannedAnnualSpend: plannedAnnualSpend,
            PricePerKwh: pricePerKwh,
            MonthlyBaseFee: monthlyBaseFee,
            ProviderName: null,
            ContractStartDate: null,
            ContractDurationMonths: null);

    [Fact]
    public void Validate_AnnualKwhBaselineAtOrAboveUpperBound_Fails()
    {
        var result = new OnboardingValidator().Validate(MakeRequest(annualKwhBaseline: 20000m));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_AnnualKwhBaselineJustUnderUpperBound_Succeeds()
    {
        var result = new OnboardingValidator().Validate(MakeRequest(annualKwhBaseline: 19999m));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_PricePerKwhAtOrAboveUpperBound_Fails()
    {
        var result = new OnboardingValidator().Validate(MakeRequest(pricePerKwh: 10m));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_MonthlyBaseFeeAtOrAboveUpperBound_Fails()
    {
        var result = new OnboardingValidator().Validate(MakeRequest(monthlyBaseFee: 1000m));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_AnnualKwhBaselineExceedsFourDecimalPlaces_Fails()
    {
        var result = new OnboardingValidator().Validate(MakeRequest(annualKwhBaseline: 3500.56789m));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_AnnualKwhBaselineWithTrailingZerosBeyondFourDecimals_Succeeds()
    {
        var result = new OnboardingValidator().Validate(MakeRequest(annualKwhBaseline: 3500.500000m));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_PricePerKwhExceedsSixDecimalPlaces_Fails()
    {
        var result = new OnboardingValidator().Validate(MakeRequest(pricePerKwh: 0.1234567m));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_PricePerKwhWithTrailingZerosBeyondSixDecimals_Succeeds()
    {
        var result = new OnboardingValidator().Validate(MakeRequest(pricePerKwh: 0.350000m));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_MonthlyBaseFeeExceedsFourDecimalPlaces_Fails()
    {
        var result = new OnboardingValidator().Validate(MakeRequest(monthlyBaseFee: 10.56789m));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_MonthlyBaseFeeWithTrailingZerosBeyondFourDecimals_Succeeds()
    {
        var result = new OnboardingValidator().Validate(MakeRequest(monthlyBaseFee: 10.500000m));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_PlannedAnnualSpendExceedsFourDecimalPlaces_Fails()
    {
        var result = new OnboardingValidator().Validate(MakeRequest(plannedAnnualSpend: 500.56789m));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_PlannedAnnualSpendAtOrAboveUpperBound_Fails()
    {
        var result = new OnboardingValidator().Validate(MakeRequest(plannedAnnualSpend: 50000m));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_PlannedAnnualSpendAboveUpperBound_Fails()
    {
        var result = new OnboardingValidator().Validate(MakeRequest(plannedAnnualSpend: 50001m));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_PlannedAnnualSpendAtOrBelowLowerBound_Fails()
    {
        var result = new OnboardingValidator().Validate(MakeRequest(plannedAnnualSpend: 0m));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_PlannedAnnualSpendBelowLowerBound_Fails()
    {
        var result = new OnboardingValidator().Validate(MakeRequest(plannedAnnualSpend: -5m));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_PlannedAnnualSpendWithFourDecimalPlaces_Succeeds()
    {
        var result = new OnboardingValidator().Validate(MakeRequest(plannedAnnualSpend: 500.5678m));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_PlannedAnnualSpendNull_Succeeds()
    {
        var result = new OnboardingValidator().Validate(MakeRequest(plannedAnnualSpend: null));

        result.IsValid.ShouldBeTrue();
    }
}
