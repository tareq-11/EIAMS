using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;

namespace Application.Organizations.GetList;

public sealed record GetOrganizationsQuery(Status? Status, int Page, int PageSize)
    : IQuery<PagedResult<OrganizationResponse>>;
