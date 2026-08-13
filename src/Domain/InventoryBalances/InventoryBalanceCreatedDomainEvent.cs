using SharedKernel;

namespace Domain.InventoryBalances;

public sealed record InventoryBalanceCreatedDomainEvent(Guid BalanceId, Guid WarehouseId, Guid MaterialId)
    : IDomainEvent;
