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
}
