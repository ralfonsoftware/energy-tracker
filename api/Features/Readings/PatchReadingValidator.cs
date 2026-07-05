using FluentValidation;

namespace EnergyTracker.Api.Features.Readings;

public class PatchReadingValidator : AbstractValidator<PatchReadingRequest>
{
    public PatchReadingValidator()
    {
        RuleFor(r => r.KwhValue).GreaterThan(0m)
            .WithMessage("kwhValue must be greater than 0.")
            .PrecisionScale(18, 4, true)
            .WithMessage("kwhValue must have at most 4 decimal places.");
    }
}
