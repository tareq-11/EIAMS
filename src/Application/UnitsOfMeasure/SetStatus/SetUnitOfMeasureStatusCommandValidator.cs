using FluentValidation;

namespace Application.UnitsOfMeasure.SetStatus;

internal sealed class SetUnitOfMeasureStatusCommandValidator : AbstractValidator<SetUnitOfMeasureStatusCommand>
{
    public SetUnitOfMeasureStatusCommandValidator()
    {
        RuleFor(c => c.UnitOfMeasureId).NotEmpty();
        RuleFor(c => c.Status).IsInEnum();
    }
}
