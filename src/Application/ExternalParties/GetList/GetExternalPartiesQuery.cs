using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;

namespace Application.ExternalParties.GetList;

public sealed record GetExternalPartiesQuery(string? Search, Status? Status, int Page, int PageSize)
    : IQuery<PagedResult<ExternalPartyResponse>>;
