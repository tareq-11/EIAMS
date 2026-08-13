using SharedKernel;

namespace Domain.InventoryBalances;

public sealed record InventoryBalanceUpdatedDomainEvent(Guid BalanceId, Guid WarehouseId, Guid MaterialId, decimal Quantity)
    : IDomainEvent;
