using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.WarehouseCapabilityOperations.GetByCapability;

public sealed record GetWarehouseCapabilityOperationsQuery(Guid CapabilityId, int Page, int PageSize)
    : IQuery<PagedResult<WarehouseCapabilityOperationResponse>>;
