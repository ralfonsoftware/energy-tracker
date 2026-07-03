using EnergyTracker.Api.Features.Flats;
using Shouldly;

namespace api.Tests.Features.Flats;

public class PatchFlatValidatorTests
{
    [Fact]
    public void Validate_AnnualKwhBaselineAtOrAboveUpperBound_Fails()
    {
        var request = new PatchFlatRequest(Name: null, AnnualKwhBaseline: 20000m, PlannedAnnualSpendProvided: false, PlannedAnnualSpend: null);

        var result = new PatchFlatValidator().Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_AnnualKwhBaselineJustUnderUpperBound_Succeeds()
    {
        var request = new PatchFlatRequest(Name: null, AnnualKwhBaseline: 19999m, PlannedAnnualSpendProvided: false, PlannedAnnualSpend: null);

        var result = new PatchFlatValidator().Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_AnnualKwhBaselineNull_Succeeds()
    {
        var request = new PatchFlatRequest(Name: "Flat", AnnualKwhBaseline: null, PlannedAnnualSpendProvided: false, PlannedAnnualSpend: null);

        var result = new PatchFlatValidator().Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_PlannedAnnualSpendAtOrAboveUpperBound_Fails()
    {
        var request = new PatchFlatRequest(Name: null, AnnualKwhBaseline: null, PlannedAnnualSpendProvided: true, PlannedAnnualSpend: 50000m);

        var result = new PatchFlatValidator().Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_PlannedAnnualSpendAtOrBelowLowerBound_Fails()
    {
        var request = new PatchFlatRequest(Name: null, AnnualKwhBaseline: null, PlannedAnnualSpendProvided: true, PlannedAnnualSpend: 0m);

        var result = new PatchFlatValidator().Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_PlannedAnnualSpendWellAboveUpperBound_Fails()
    {
        var request = new PatchFlatRequest(Name: null, AnnualKwhBaseline: null, PlannedAnnualSpendProvided: true, PlannedAnnualSpend: 75000m);

        var result = new PatchFlatValidator().Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_PlannedAnnualSpendNegative_Fails()
    {
        var request = new PatchFlatRequest(Name: null, AnnualKwhBaseline: null, PlannedAnnualSpendProvided: true, PlannedAnnualSpend: -100m);

        var result = new PatchFlatValidator().Validate(request);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_PlannedAnnualSpendJustUnderUpperBound_Succeeds()
    {
        var request = new PatchFlatRequest(Name: null, AnnualKwhBaseline: null, PlannedAnnualSpendProvided: true, PlannedAnnualSpend: 49999m);

        var result = new PatchFlatValidator().Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_PlannedAnnualSpendNotProvided_Succeeds()
    {
        var request = new PatchFlatRequest(Name: null, AnnualKwhBaseline: null, PlannedAnnualSpendProvided: false, PlannedAnnualSpend: null);

        var result = new PatchFlatValidator().Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_PlannedAnnualSpendExplicitNullClear_Succeeds()
    {
        var request = new PatchFlatRequest(Name: null, AnnualKwhBaseline: null, PlannedAnnualSpendProvided: true, PlannedAnnualSpend: null);

        var result = new PatchFlatValidator().Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}
