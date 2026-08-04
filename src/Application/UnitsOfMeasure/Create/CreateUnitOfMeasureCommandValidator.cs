using FluentValidation;

namespace Application.UnitsOfMeasure.Create;

internal sealed class CreateUnitOfMeasureCommandValidator : AbstractValidator<CreateUnitOfMeasureCommand>
{
    public CreateUnitOfMeasureCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Symbol).NotEmpty().MaximumLength(20);
        RuleFor(c => c.UnitType).NotEmpty().MaximumLength(50);
    }
}
