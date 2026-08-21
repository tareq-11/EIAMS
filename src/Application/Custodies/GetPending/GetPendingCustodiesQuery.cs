using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.Custodies.GetPending;

public sealed record GetPendingCustodiesQuery(Guid WarehouseId, int Page, int PageSize)
    : IQuery<PagedResult<PendingCustodyResponse>>;
