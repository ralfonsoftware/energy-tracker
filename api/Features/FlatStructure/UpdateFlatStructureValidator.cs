using EnergyTracker.Api.Data.Entities;
using FluentValidation;

namespace EnergyTracker.Api.Features.FlatStructure;

public class UpdateFlatStructureValidator : AbstractValidator<UpdateFlatStructureRequest>
{
    public UpdateFlatStructureValidator()
    {
        RuleFor(r => r.Rooms).NotNull();
        RuleForEach(r => r.Rooms).ChildRules(room =>
        {
            room.RuleFor(rm => rm.Name).NotEmpty().MaximumLength(200);
            room.RuleFor(rm => rm.PowerPoints).NotNull();
            room.RuleForEach(rm => rm.PowerPoints).ChildRules(pp =>
            {
                pp.RuleFor(p => p.Name).NotEmpty().MaximumLength(200);
                pp.RuleFor(p => p.PlugId).MaximumLength(200);
                pp.RuleFor(p => p.Devices).NotNull();
                pp.RuleForEach(p => p.Devices).ChildRules(d =>
                {
                    d.RuleFor(dv => dv.Name).NotEmpty().MaximumLength(200);
                    d.RuleFor(dv => dv.Type).MaximumLength(200);
                    d.RuleFor(dv => dv.Manufacturer).MaximumLength(200);
                    d.RuleFor(dv => dv.Model).MaximumLength(200);
                    d.RuleFor(dv => dv.EuLabelClass).MaximumLength(200);
                    d.RuleFor(dv => dv.ConsumptionApproach).IsInEnum();
                    d.RuleFor(dv => dv.SelfMeasuredPeriod).IsInEnum();
                    d.RuleFor(dv => dv.EuAnnualKwh).GreaterThanOrEqualTo(0)
                        .When(dv => dv.EuAnnualKwh.HasValue);
                    d.RuleFor(dv => dv.SelfMeasuredKwh).GreaterThanOrEqualTo(0)
                        .When(dv => dv.SelfMeasuredKwh.HasValue);
                    d.RuleFor(dv => dv.EuLabelClass).NotEmpty()
                        .When(dv => dv.ConsumptionApproach == ConsumptionApproach.EuLabel);
                    d.RuleFor(dv => dv.EuAnnualKwh).NotNull()
                        .When(dv => dv.ConsumptionApproach == ConsumptionApproach.EuLabel);
                    d.RuleFor(dv => dv.SelfMeasuredKwh).NotNull()
                        .When(dv => dv.ConsumptionApproach == ConsumptionApproach.SelfMeasured);
                    d.RuleFor(dv => dv.SelfMeasuredPeriod).NotNull()
                        .When(dv => dv.ConsumptionApproach == ConsumptionApproach.SelfMeasured);
                });
            });
        });
    }
}
