using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using FluentValidation;

namespace EnergyTracker.Api.Features.FlatStructure;

public class DeviceValidator : AbstractValidator<DeviceInput>
{
    public DeviceValidator()
    {
        RuleFor(d => d.Name).NotEmpty().MaximumLength(200);
        RuleFor(d => d.Type).MaximumLength(200);
        RuleFor(d => d.Manufacturer).MaximumLength(200);
        RuleFor(d => d.Model).MaximumLength(200);
        RuleFor(d => d.EuLabelClass).MaximumLength(200);
        RuleFor(d => d.ConsumptionApproach).IsInEnum();
        RuleFor(d => d.SelfMeasuredPeriod).IsInEnum();
        RuleFor(d => d.EuAnnualKwh).GreaterThanOrEqualTo(0)
            .DecimalPrecision(4)
            .WithMessage("euAnnualKwh must have at most 4 decimal places.")
            .When(d => d.EuAnnualKwh.HasValue);
        RuleFor(d => d.SelfMeasuredKwh).GreaterThanOrEqualTo(0)
            .DecimalPrecision(4)
            .WithMessage("selfMeasuredKwh must have at most 4 decimal places.")
            .When(d => d.SelfMeasuredKwh.HasValue);
        RuleFor(d => d.EuAnnualKwh).NotNull()
            .When(d => d.ConsumptionApproach == ConsumptionApproach.EuLabel);
        RuleFor(d => d.SelfMeasuredKwh).NotNull()
            .When(d => d.ConsumptionApproach == ConsumptionApproach.SelfMeasured);
        RuleFor(d => d.SelfMeasuredPeriod).NotNull()
            .When(d => d.ConsumptionApproach == ConsumptionApproach.SelfMeasured);
        RuleFor(d => d.DecommissionedDate).GreaterThanOrEqualTo(d => d.InUseSince)
            .When(d => d.InUseSince.HasValue && d.DecommissionedDate.HasValue)
            .WithMessage("decommissionedDate must not be before inUseSince.");
    }
}
