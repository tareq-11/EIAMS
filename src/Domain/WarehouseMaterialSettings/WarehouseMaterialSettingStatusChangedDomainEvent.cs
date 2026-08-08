using Domain.Common;
using SharedKernel;

namespace Domain.WarehouseMaterialSettings;

public sealed record WarehouseMaterialSettingStatusChangedDomainEvent(Guid SettingId, Status Status) : IDomainEvent;
