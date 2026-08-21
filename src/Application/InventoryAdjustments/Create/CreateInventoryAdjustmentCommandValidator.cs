using FluentValidation;

namespace Application.InventoryAdjustments.Create;

internal sealed class CreateInventoryAdjustmentCommandValidator : AbstractValidator<CreateInventoryAdjustmentCommand>
{
    public CreateInventoryAdjustmentCommandValidator()
    {
        RuleFor(command => command.WarehouseId).NotEmpty();
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(500);
    }
}
