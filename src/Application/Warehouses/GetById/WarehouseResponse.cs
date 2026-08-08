namespace Application.Warehouses.GetById;

public sealed class WarehouseResponse
{
    public Guid Id { get; init; }

    public Guid SiteId { get; init; }

    public string Name { get; init; }

    public string Code { get; init; }

    public string WarehouseType { get; init; }

    public bool CanHoldStock { get; init; }

    public string Status { get; init; }

    public int RowVersion { get; init; }
}
