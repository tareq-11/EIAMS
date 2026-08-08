using FluentValidation;

namespace Application.WarehouseCapabilityOperations.AddOperation;

internal sealed class AddWarehouseCapabilityOperationCommandValidator
    : AbstractValidator<AddWarehouseCapabilityOperationCommand>
{
    public AddWarehouseCapabilityOperationCommandValidator()
    {
        RuleFor(c => c.CapabilityId).NotEmpty();
        RuleFor(c => c.OperationType).IsInEnum();
    }
}
