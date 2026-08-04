using FluentValidation;

namespace Application.MaterialFamilies.Create;

internal sealed class CreateMaterialFamilyCommandValidator : AbstractValidator<CreateMaterialFamilyCommand>
{
    public CreateMaterialFamilyCommandValidator()
    {
        RuleFor(c => c.CategoryId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Code).NotEmpty().MaximumLength(50);
        RuleFor(c => c.BaseUnitId).NotEmpty();
    }
}
