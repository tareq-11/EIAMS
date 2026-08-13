namespace Domain.Common;

/// <summary>
/// The kind of signed quantity delta a StockMovement records (PRD 10.4, D-MOV-01). Receiving/Opening
/// produce positive deltas; Issue/TransferOut/AdjustmentOut produce negative deltas;
/// TransferIn/AdjustmentIn are positive.
/// </summary>
public enum MovementType
{
    Receipt,
    Issue,
    TransferIn,
    TransferOut,
    AdjustmentIn,
    AdjustmentOut,
    Opening
}
