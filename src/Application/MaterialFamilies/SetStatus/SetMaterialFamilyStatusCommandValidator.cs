using FluentValidation;

namespace Application.MaterialFamilies.SetStatus;

internal sealed class SetMaterialFamilyStatusCommandValidator : AbstractValidator<SetMaterialFamilyStatusCommand>
{
    public SetMaterialFamilyStatusCommandValidator()
    {
        RuleFor(c => c.MaterialFamilyId).NotEmpty();
        RuleFor(c => c.Status).IsInEnum();
    }
}
