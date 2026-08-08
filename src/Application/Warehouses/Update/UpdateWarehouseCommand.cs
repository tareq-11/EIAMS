using Application.Abstractions.Messaging;

namespace Application.Warehouses.Update;

public sealed record UpdateWarehouseCommand(
    Guid WarehouseId,
    string Name,
    string WarehouseType,
    bool CanHoldStock,
    int ExpectedRowVersion) : ICommand;
