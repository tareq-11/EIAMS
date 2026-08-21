using FluentValidation;

namespace Application.InventoryCounts.SetVarianceReason;

internal sealed class SetInventoryCountVarianceReasonCommandValidator : AbstractValidator<SetInventoryCountVarianceReasonCommand>
{
    public SetInventoryCountVarianceReasonCommandValidator()
    {
        RuleFor(command => command.CountId).NotEmpty();
        RuleFor(command => command.LineId).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(200);
        RuleFor(command => command.ExpectedRowVersion).GreaterThan(0);
    }
}
