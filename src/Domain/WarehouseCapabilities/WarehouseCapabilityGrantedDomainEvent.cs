using SharedKernel;

namespace Domain.WarehouseCapabilities;

public sealed record WarehouseCapabilityGrantedDomainEvent(Guid CapabilityId, Guid WarehouseId, Guid MaterialDomainId)
    : IDomainEvent;
