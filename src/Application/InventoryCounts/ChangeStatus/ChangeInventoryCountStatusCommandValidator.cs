using FluentValidation;

namespace Application.InventoryCounts.ChangeStatus;

internal sealed class ChangeInventoryCountStatusCommandValidator : AbstractValidator<ChangeInventoryCountStatusCommand>
{
    public ChangeInventoryCountStatusCommandValidator()
    {
        RuleFor(command => command.CountId).NotEmpty();
        RuleFor(command => command.TargetStatus).IsInEnum();
        RuleFor(command => command.ExpectedRowVersion).GreaterThan(0);
    }
}
