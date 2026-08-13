using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.MaterialUnitConversions.GetByMaterial;

public sealed record GetMaterialUnitConversionsQuery(Guid MaterialId, int Page, int PageSize)
    : IQuery<PagedResult<MaterialUnitConversionResponse>>;
