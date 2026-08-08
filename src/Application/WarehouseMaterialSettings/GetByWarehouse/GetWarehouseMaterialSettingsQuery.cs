using Application.Abstractions.Messaging;

namespace Application.WarehouseMaterialSettings.GetByWarehouse;

public sealed record GetWarehouseMaterialSettingsQuery(Guid WarehouseId)
    : IQuery<List<WarehouseMaterialSettingResponse>>;
