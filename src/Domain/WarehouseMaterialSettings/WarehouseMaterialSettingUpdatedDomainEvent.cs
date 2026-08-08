using SharedKernel;

namespace Domain.WarehouseMaterialSettings;

public sealed record WarehouseMaterialSettingUpdatedDomainEvent(Guid SettingId) : IDomainEvent;
