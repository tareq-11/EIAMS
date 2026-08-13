using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;

namespace Application.Sites.GetList;

public sealed record GetSitesQuery(Guid? OrganizationId, Status? Status, int Page, int PageSize)
    : IQuery<PagedResult<SiteResponse>>;
