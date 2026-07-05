using FluentValidation;

namespace EnergyTracker.Api.Features.Onboarding;

public class OnboardingValidator : AbstractValidator<CompleteOnboardingRequest>
{
    public OnboardingValidator()
    {
        RuleFor(r => r.FlatName).NotEmpty().MaximumLength(200);
        RuleFor(r => r.AnnualKwhBaseline).GreaterThan(0).LessThan(20000)
            .WithMessage("annualKwhBaseline must be less than 20000.")
            .PrecisionScale(18, 4, true)
            .WithMessage("annualKwhBaseline must have at most 4 decimal places.");
        RuleFor(r => r.PricePerKwh).GreaterThan(0).LessThan(10)
            .WithMessage("pricePerKwh must be less than 10.")
            .PrecisionScale(18, 6, true)
            .WithMessage("pricePerKwh must have at most 6 decimal places.");
        RuleFor(r => r.MonthlyBaseFee).GreaterThanOrEqualTo(0).LessThan(1000)
            .WithMessage("monthlyBaseFee must be less than 1000.")
            .PrecisionScale(18, 4, true)
            .WithMessage("monthlyBaseFee must have at most 4 decimal places.");
        RuleFor(r => r.ProviderName).MaximumLength(200).When(r => r.ProviderName != null);
        RuleFor(r => r.ContractDurationMonths).InclusiveBetween(1, 60).When(r => r.ContractDurationMonths.HasValue);
    }
}
