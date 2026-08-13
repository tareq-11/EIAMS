using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;

namespace Application.MaterialDomains.GetList;

public sealed record GetMaterialDomainsQuery(Status? Status, int Page, int PageSize)
    : IQuery<PagedResult<MaterialDomainResponse>>;
