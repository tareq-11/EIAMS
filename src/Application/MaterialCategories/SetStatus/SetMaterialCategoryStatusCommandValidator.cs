using FluentValidation;

namespace Application.MaterialCategories.SetStatus;

internal sealed class SetMaterialCategoryStatusCommandValidator : AbstractValidator<SetMaterialCategoryStatusCommand>
{
    public SetMaterialCategoryStatusCommandValidator()
    {
        RuleFor(c => c.MaterialCategoryId).NotEmpty();
        RuleFor(c => c.Status).IsInEnum();
    }
}
