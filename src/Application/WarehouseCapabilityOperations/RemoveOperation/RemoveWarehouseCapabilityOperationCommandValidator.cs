using FluentValidation;

namespace Application.WarehouseCapabilityOperations.RemoveOperation;

internal sealed class RemoveWarehouseCapabilityOperationCommandValidator
    : AbstractValidator<RemoveWarehouseCapabilityOperationCommand>
{
    public RemoveWarehouseCapabilityOperationCommandValidator()
    {
        RuleFor(c => c.CapabilityId).NotEmpty();
        RuleFor(c => c.OperationType).IsInEnum();
    }
}
