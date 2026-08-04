using FluentValidation;

namespace Application.Materials.SetStatus;

internal sealed class SetMaterialStatusCommandValidator : AbstractValidator<SetMaterialStatusCommand>
{
    public SetMaterialStatusCommandValidator()
    {
        RuleFor(c => c.MaterialId).NotEmpty();
        RuleFor(c => c.Status).IsInEnum();
    }
}
