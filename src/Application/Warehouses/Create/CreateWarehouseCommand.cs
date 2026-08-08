using Application.Abstractions.Messaging;

namespace Application.Warehouses.Create;

public sealed record CreateWarehouseCommand(
    Guid SiteId,
    string Name,
    string Code,
    string WarehouseType,
    bool CanHoldStock) : ICommand<Guid>;
