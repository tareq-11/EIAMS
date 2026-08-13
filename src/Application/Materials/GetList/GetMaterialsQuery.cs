using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Materials;

namespace Application.Materials.GetList;

public sealed record GetMaterialsQuery(
    Guid? FamilyId,
    Guid? MaterialDomainId,
    MaterialStatus? Status,
    int Page,
    int PageSize)
    : IQuery<PagedResult<MaterialResponse>>;
