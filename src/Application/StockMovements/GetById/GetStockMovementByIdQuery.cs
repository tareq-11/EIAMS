using Application.Abstractions.Messaging;
using Application.StockMovements.GetList;

namespace Application.StockMovements.GetById;

public sealed record GetStockMovementByIdQuery(Guid MovementId) : IQuery<StockMovementResponse>;
