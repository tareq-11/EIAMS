using Application.Abstractions.Messaging;

namespace Application.Warehouses.Create;

public sealed record CreateWarehouseCommand(
    Guid SiteId,
    Guid OrganizationalUnitId,
    string Name,
    string Code,
    string WarehouseType,
    bool CanHoldStock) : ICommand<Guid>
{
    public CreateWarehouseCommand(
        Guid siteId,
        string name,
        string code,
        string warehouseType,
        bool canHoldStock)
        : this(siteId, Guid.Empty, name, code, warehouseType, canHoldStock)
    {
    }
}
