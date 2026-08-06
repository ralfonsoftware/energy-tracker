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
            });
        });
    }
}
