using FluentValidation;

namespace Application.MaterialFamilies.Update;

internal sealed class UpdateMaterialFamilyCommandValidator : AbstractValidator<UpdateMaterialFamilyCommand>
{
    public UpdateMaterialFamilyCommandValidator()
    {
        RuleFor(c => c.MaterialFamilyId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Code).NotEmpty().MaximumLength(50);
    }
}
