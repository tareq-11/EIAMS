using Application.Abstractions.Messaging;

namespace Application.InventoryCounts.GetById;

public sealed record GetInventoryCountByIdQuery(Guid CountId) : IQuery<InventoryCountDetailsResponse>;
