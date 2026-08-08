namespace Application.WarehouseCapabilities.GetByWarehouse;

public sealed class WarehouseCapabilityResponse
{
    public Guid Id { get; init; }

    public Guid WarehouseId { get; init; }

    public Guid MaterialDomainId { get; init; }

    public string MaterialDomainCode { get; init; }

    public string MaterialDomainName { get; init; }

    public string Status { get; init; }
}
