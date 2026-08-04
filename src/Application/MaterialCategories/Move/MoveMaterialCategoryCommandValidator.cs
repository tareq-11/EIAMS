using FluentValidation;

namespace Application.MaterialCategories.Move;

internal sealed class MoveMaterialCategoryCommandValidator : AbstractValidator<MoveMaterialCategoryCommand>
{
    public MoveMaterialCategoryCommandValidator()
    {
        RuleFor(command => command.MaterialCategoryId).NotEmpty();
        RuleFor(command => command.ParentCategoryId)
            .NotEqual(command => command.MaterialCategoryId)
            .When(command => command.ParentCategoryId.HasValue)
            .WithMessage("A category cannot be its own parent.");
    }
}
