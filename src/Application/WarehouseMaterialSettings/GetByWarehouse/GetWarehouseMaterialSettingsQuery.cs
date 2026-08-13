using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.WarehouseMaterialSettings.GetByWarehouse;

public sealed record GetWarehouseMaterialSettingsQuery(Guid WarehouseId, int Page, int PageSize)
    : IQuery<PagedResult<WarehouseMaterialSettingResponse>>;
