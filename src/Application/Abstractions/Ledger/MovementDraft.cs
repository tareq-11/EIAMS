using Domain.Common;

namespace Application.Abstractions.Ledger;

/// <summary>
/// One movement a posting strategy wants appended to the ledger, before it is turned into a
/// persisted <c>StockMovement</c> row by <see cref="IInventoryLedgerWriter"/>.
/// </summary>
public sealed record MovementDraft(
    Guid WarehouseId,
    Guid MaterialId,
    Guid DocumentId,
    Guid LineId,
    MovementType MovementType,
    decimal QuantityDelta);
