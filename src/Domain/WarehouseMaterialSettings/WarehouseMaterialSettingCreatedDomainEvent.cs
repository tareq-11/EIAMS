using SharedKernel;

namespace Domain.WarehouseMaterialSettings;

public sealed record WarehouseMaterialSettingCreatedDomainEvent(Guid SettingId, Guid WarehouseId, Guid MaterialId)
    : IDomainEvent;
