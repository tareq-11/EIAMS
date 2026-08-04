using FluentValidation;

namespace Application.MaterialCategories.Update;

internal sealed class UpdateMaterialCategoryCommandValidator : AbstractValidator<UpdateMaterialCategoryCommand>
{
    public UpdateMaterialCategoryCommandValidator()
    {
        RuleFor(c => c.MaterialCategoryId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Code).NotEmpty().MaximumLength(50);
    }
}
