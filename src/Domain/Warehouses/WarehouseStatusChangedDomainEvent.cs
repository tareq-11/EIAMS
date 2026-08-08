using Domain.Common;
using SharedKernel;

namespace Domain.Warehouses;

public sealed record WarehouseStatusChangedDomainEvent(Guid WarehouseId, Status Status) : IDomainEvent;
