using Application.Abstractions.Messaging;

namespace Application.Warehouses.Update;

public sealed record UpdateWarehouseCommand(
    Guid WarehouseId,
    Guid OrganizationalUnitId,
    string Name,
    string WarehouseType,
    bool CanHoldStock,
    int ExpectedRowVersion) : ICommand
{
    public UpdateWarehouseCommand(
        Guid warehouseId,
        string name,
        string warehouseType,
        bool canHoldStock,
        int expectedRowVersion)
        : this(warehouseId, Guid.Empty, name, warehouseType, canHoldStock, expectedRowVersion)
    {
    }
}
