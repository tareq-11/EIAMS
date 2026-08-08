using Domain.Common;
using SharedKernel;

namespace Domain.WarehouseCapabilities;

public sealed record WarehouseCapabilityStatusChangedDomainEvent(
    Guid CapabilityId,
    Guid WarehouseId,
    Guid MaterialDomainId,
    Status Status) : IDomainEvent;
