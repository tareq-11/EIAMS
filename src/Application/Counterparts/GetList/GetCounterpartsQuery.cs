using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Abstractions.Recipients;
using Domain.Common;

namespace Application.Counterparts.GetList;

public sealed record GetCounterpartsQuery(
    string? Search,
    PartyType? Type,
    int Page,
    int PageSize) : IQuery<PagedResult<CounterpartResolution>>;
