using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;

namespace Application.OrganizationalUnits.GetList;

public sealed record GetOrganizationalUnitsQuery(
    Guid? SiteId,
    Guid? ParentId,
    Status? Status,
    int Page,
    int PageSize)
    : IQuery<PagedResult<OrganizationalUnitResponse>>;
