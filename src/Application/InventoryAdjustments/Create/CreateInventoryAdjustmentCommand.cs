using Application.Abstractions.Messaging;

namespace Application.InventoryAdjustments.Create;

public sealed record CreateInventoryAdjustmentCommand(Guid WarehouseId, string Reason) : ICommand<Guid>;
