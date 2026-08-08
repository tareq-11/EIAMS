using Domain.Common;
using SharedKernel;

namespace Domain.WarehouseCapabilityOperations;

public static class WarehouseCapabilityOperationErrors
{
    public static Error NotFound(Guid capabilityOperationId) => Error.NotFound(
        "WarehouseCapabilityOperations.NotFound",
        $"The warehouse capability operation with the Id = '{capabilityOperationId}' was not found",
        new { capability_operation_id = capabilityOperationId });

    public static Error AlreadyGranted(Guid capabilityId, OperationType operationType) => Error.Conflict(
        "WarehouseCapabilityOperations.AlreadyGranted",
        "This operation is already granted for the capability.",
        new { capability_id = capabilityId, operation_type = operationType.ToString() });

    public static Error CapabilityInactive(Guid capabilityId) => Error.Problem(
        "WarehouseCapabilityOperations.CapabilityInactive",
        "Operations can only be added to or removed from an active capability.",
        new { capability_id = capabilityId });

    public static Error OperationNotGranted(Guid capabilityId, OperationType operationType) => Error.Problem(
        "WarehouseCapabilityOperations.OperationNotGranted",
        $"The capability with the Id = '{capabilityId}' does not allow the '{operationType}' operation.",
        new { capability_id = capabilityId, operation_type = operationType.ToString() });

    public static readonly Error Forbidden = Error.Forbidden(
        "WarehouseCapabilityOperations.Forbidden",
        "You are not authorized to manage warehouse capability operations.");
}
