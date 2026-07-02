using EnergyTracker.Api.Features.Onboarding;
using Shouldly;

namespace api.Tests.Features.Onboarding;

public class OnboardingValidatorTests
{
    private static CompleteOnboardingRequest MakeRequest(
        decimal annualKwhBaseline = 3500m, decimal pricePerKwh = 0.35m, decimal monthlyBaseFee = 10m) =>
        new(
            FlatName: "Test Flat",
            AnnualKwhBaseline: annualKwhBaseline,
            PlannedAnnualSpend: null,
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
}
