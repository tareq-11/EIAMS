using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.InventoryCounts.Plan;

public sealed record PlanInventoryCountCommand(
    Guid WarehouseId,
    InventoryCountType CountType,
    InventoryCountScopeType ScopeType,
    Guid? MaterialDomainId,
    IReadOnlyCollection<Guid> MaterialIds,
    FreezePolicy FreezePolicy) : ICommand<Guid>;
