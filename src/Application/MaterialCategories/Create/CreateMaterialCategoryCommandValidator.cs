using FluentValidation;

namespace Application.MaterialCategories.Create;

internal sealed class CreateMaterialCategoryCommandValidator : AbstractValidator<CreateMaterialCategoryCommand>
{
    public CreateMaterialCategoryCommandValidator()
    {
        RuleFor(c => c.MaterialDomainId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Code).NotEmpty().MaximumLength(50);
    }
}
