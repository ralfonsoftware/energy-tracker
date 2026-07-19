using EnergyTracker.Api.Shared;
using FluentValidation;

namespace EnergyTracker.Api.Features.Flats;

public class CreateFlatValidator : AbstractValidator<CreateFlatRequest>
{
    public CreateFlatValidator()
    {
        RuleFor(r => r.Name).NotEmpty().WithMessage("name is required.").MaximumLength(200);
        RuleFor(r => r.AnnualKwhBaseline).GreaterThan(0).LessThan(20000)
            .WithMessage("annualKwhBaseline must be less than 20000.")
            .DecimalPrecision(4)
            .WithMessage("annualKwhBaseline must have at most 4 decimal places.");
        RuleFor(r => r.PlannedAnnualSpend).GreaterThan(0m).LessThan(50000m)
            .WithMessage("plannedAnnualSpend must be greater than 0 and less than 50000.")
            .DecimalPrecision(4)
            .WithMessage("plannedAnnualSpend must have at most 4 decimal places.")
            .When(r => r.PlannedAnnualSpend is not null);
    }
}
