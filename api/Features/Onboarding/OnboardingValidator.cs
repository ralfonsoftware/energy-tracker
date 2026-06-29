using FluentValidation;

namespace EnergyTracker.Api.Features.Onboarding;

public class OnboardingValidator : AbstractValidator<CompleteOnboardingRequest>
{
    public OnboardingValidator()
    {
        RuleFor(r => r.FlatName).NotEmpty().MaximumLength(200);
        RuleFor(r => r.AnnualKwhBaseline).GreaterThan(0);
        RuleFor(r => r.PricePerKwh).GreaterThan(0);
        RuleFor(r => r.MonthlyBaseFee).GreaterThanOrEqualTo(0);
        RuleFor(r => r.ProviderName).MaximumLength(200).When(r => r.ProviderName != null);
        RuleFor(r => r.ContractDurationMonths).InclusiveBetween(1, 60).When(r => r.ContractDurationMonths.HasValue);
    }
}
