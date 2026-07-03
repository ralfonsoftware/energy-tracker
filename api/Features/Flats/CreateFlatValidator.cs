using FluentValidation;

namespace EnergyTracker.Api.Features.Flats;

public class CreateFlatValidator : AbstractValidator<CreateFlatRequest>
{
    public CreateFlatValidator()
    {
        RuleFor(r => r.Name).NotEmpty().WithMessage("name is required.").MaximumLength(200);
        RuleFor(r => r.AnnualKwhBaseline).GreaterThan(0).LessThan(20000)
            .WithMessage("annualKwhBaseline must be less than 20000.");
        RuleFor(r => r.PlannedAnnualSpend).GreaterThan(0m).LessThan(50000m)
            .WithMessage("plannedAnnualSpend must be greater than 0 and less than 50000.")
            .When(r => r.PlannedAnnualSpend is not null);
    }
}
