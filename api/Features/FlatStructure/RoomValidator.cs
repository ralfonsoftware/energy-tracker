using FluentValidation;

namespace EnergyTracker.Api.Features.FlatStructure;

public class CreateRoomRequestValidator : AbstractValidator<CreateRoomRequest>
{
    public CreateRoomRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.PowerPoints).NotNull();
        RuleForEach(r => r.PowerPoints).ChildRules(pp =>
        {
            pp.RuleFor(p => p.Name).NotEmpty().MaximumLength(200);
            pp.RuleFor(p => p.PlugId).MaximumLength(200);
        });
    }
}

public class UpdateRoomRequestValidator : AbstractValidator<UpdateRoomRequest>
{
    public UpdateRoomRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.PowerPoints).NotNull();
        RuleForEach(r => r.PowerPoints).ChildRules(pp =>
        {
            pp.RuleFor(p => p.Name).NotEmpty().MaximumLength(200);
            pp.RuleFor(p => p.PlugId).MaximumLength(200);
        });
    }
}
