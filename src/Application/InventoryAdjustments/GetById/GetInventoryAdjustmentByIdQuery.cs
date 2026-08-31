using Application.Abstractions.Messaging;

namespace Application.InventoryAdjustments.GetById;

public sealed record GetInventoryAdjustmentByIdQuery(Guid AdjustmentId) : IQuery<InventoryAdjustmentDetailsResponse>;
