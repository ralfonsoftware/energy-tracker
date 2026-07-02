using FluentValidation;

namespace EnergyTracker.Api.Features.Tariffs;

public class TariffValidator : AbstractValidator<CreateTariffRequest>
{
    public TariffValidator()
    {
        RuleFor(r => r.EffectiveDate).NotNull().WithMessage("effectiveDate is required.");
        RuleFor(r => r.PricePerKwh).GreaterThan(0m).LessThan(10m)
            .WithMessage("pricePerKwh must be less than 10.");
        RuleFor(r => r.MonthlyBaseFee).GreaterThanOrEqualTo(0m).LessThan(1000m)
            .WithMessage("monthlyBaseFee must be less than 1000.");
        RuleFor(r => r.ProviderName).MaximumLength(200).When(r => r.ProviderName != null);
        RuleFor(r => r.ContractDurationMonths).InclusiveBetween(1, 60).When(r => r.ContractDurationMonths.HasValue);
    }
}
