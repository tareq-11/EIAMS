using FluentValidation;

namespace Application.InventoryAdjustments.UpdateLine;

internal sealed class UpdateAdjustmentLineCommandValidator : AbstractValidator<UpdateAdjustmentLineCommand>
{
    public UpdateAdjustmentLineCommandValidator()
    {
        RuleFor(command => command.DocumentId).NotEmpty();
        RuleFor(command => command.LineId).NotEmpty();
        RuleFor(command => command.Difference).NotEqual(0);
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(200);
        RuleFor(command => command.ExpectedRowVersion).GreaterThan(0);
    }
}
