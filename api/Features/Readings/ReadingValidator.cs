using EnergyTracker.Api.Shared;
using FluentValidation;

namespace EnergyTracker.Api.Features.Readings;

public class ReadingValidator : AbstractValidator<SubmitReadingRequest>
{
    public ReadingValidator()
    {
        RuleFor(r => r.KwhValue).GreaterThan(0m)
            .WithMessage("kwhValue must be greater than 0.")
            .DecimalPrecision(4)
            .WithMessage("kwhValue must have at most 4 decimal places.");
        RuleFor(r => r.ReadingDate).NotNull()
            .WithMessage("readingDate is required.");
    }
}
