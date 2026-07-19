using EnergyTracker.Api.Shared;
using FluentValidation;

namespace EnergyTracker.Api.Features.Onboarding;

public class OnboardingValidator : AbstractValidator<CompleteOnboardingRequest>
{
    public OnboardingValidator()
    {
        RuleFor(r => r.FlatName).NotEmpty().MaximumLength(200);
        RuleFor(r => r.AnnualKwhBaseline).GreaterThan(0).LessThan(20000)
            .WithMessage("annualKwhBaseline must be less than 20000.")
            .DecimalPrecision(4)
            .WithMessage("annualKwhBaseline must have at most 4 decimal places.");
        RuleFor(r => r.PricePerKwh).GreaterThan(0).LessThan(10)
            .WithMessage("pricePerKwh must be less than 10.")
            .DecimalPrecision(6)
            .WithMessage("pricePerKwh must have at most 6 decimal places.");
        RuleFor(r => r.MonthlyBaseFee).GreaterThanOrEqualTo(0).LessThan(1000)
            .WithMessage("monthlyBaseFee must be less than 1000.")
            .DecimalPrecision(4)
            .WithMessage("monthlyBaseFee must have at most 4 decimal places.");
        RuleFor(r => r.ProviderName).MaximumLength(200).When(r => r.ProviderName != null);
        RuleFor(r => r.ContractDurationMonths).InclusiveBetween(1, 60).When(r => r.ContractDurationMonths.HasValue);
        RuleFor(r => r.PlannedAnnualSpend).GreaterThan(0m).LessThan(50000m)
            .WithMessage("plannedAnnualSpend must be greater than 0 and less than 50000.")
            .DecimalPrecision(4)
            .WithMessage("plannedAnnualSpend must have at most 4 decimal places.")
            .When(r => r.PlannedAnnualSpend is not null);
    }
}
