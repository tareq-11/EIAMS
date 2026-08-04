using FluentValidation;

namespace Application.UnitsOfMeasure.Update;

internal sealed class UpdateUnitOfMeasureCommandValidator : AbstractValidator<UpdateUnitOfMeasureCommand>
{
    public UpdateUnitOfMeasureCommandValidator()
    {
        RuleFor(c => c.UnitOfMeasureId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.Symbol).NotEmpty().MaximumLength(20);
        RuleFor(c => c.UnitType).NotEmpty().MaximumLength(50);
    }
}
