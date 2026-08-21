using FluentValidation;

namespace Application.InventoryAdjustments.RemoveLine;

internal sealed class RemoveAdjustmentLineCommandValidator : AbstractValidator<RemoveAdjustmentLineCommand>
{
    public RemoveAdjustmentLineCommandValidator()
    {
        RuleFor(command => command.DocumentId).NotEmpty();
        RuleFor(command => command.LineId).NotEmpty();
        RuleFor(command => command.ExpectedRowVersion).GreaterThan(0);
    }
}
