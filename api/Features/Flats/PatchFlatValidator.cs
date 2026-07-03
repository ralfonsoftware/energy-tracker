using FluentValidation;

namespace EnergyTracker.Api.Features.Flats;

public class PatchFlatValidator : AbstractValidator<PatchFlatRequest>
{
    public PatchFlatValidator()
    {
        RuleFor(r => r.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Name must not be empty or whitespace.")
            .MaximumLength(200)
            .When(r => r.Name is not null);
        RuleFor(r => r.AnnualKwhBaseline).GreaterThan(0).LessThan(20000)
            .WithMessage("annualKwhBaseline must be less than 20000.")
            .When(r => r.AnnualKwhBaseline is not null);
        RuleFor(r => r.PlannedAnnualSpend).GreaterThan(0m).LessThan(50000m)
            .WithMessage("plannedAnnualSpend must be greater than 0 and less than 50000.")
            .When(r => r.PlannedAnnualSpendProvided && r.PlannedAnnualSpend is not null);
    }
}
