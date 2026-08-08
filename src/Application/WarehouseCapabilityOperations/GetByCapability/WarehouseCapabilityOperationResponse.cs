namespace Application.WarehouseCapabilityOperations.GetByCapability;

public sealed class WarehouseCapabilityOperationResponse
{
    public Guid Id { get; init; }

    public Guid CapabilityId { get; init; }

    public string OperationType { get; init; }
}
