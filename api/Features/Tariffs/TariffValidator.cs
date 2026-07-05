using FluentValidation;

namespace EnergyTracker.Api.Features.Tariffs;

public class TariffValidator : AbstractValidator<CreateTariffRequest>
{
    public TariffValidator()
    {
        RuleFor(r => r.ContractStartDate).NotNull().WithMessage("contractStartDate is required.");
        RuleFor(r => r.PricePerKwh).GreaterThan(0m).LessThan(10m)
            .WithMessage("pricePerKwh must be less than 10.")
            .PrecisionScale(18, 6, true)
            .WithMessage("pricePerKwh must have at most 6 decimal places.");
        RuleFor(r => r.MonthlyBaseFee).GreaterThanOrEqualTo(0m).LessThan(1000m)
            .WithMessage("monthlyBaseFee must be less than 1000.")
            .PrecisionScale(18, 4, true)
            .WithMessage("monthlyBaseFee must have at most 4 decimal places.");
        RuleFor(r => r.ProviderName).MaximumLength(200).When(r => r.ProviderName != null);
        RuleFor(r => r.ContractDurationMonths).InclusiveBetween(1, 60).When(r => r.ContractDurationMonths.HasValue);
    }
}
