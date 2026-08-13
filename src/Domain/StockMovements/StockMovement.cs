using Domain.Common;
using SharedKernel;

namespace Domain.StockMovements;

/// <summary>
/// One append-only ledger entry (Ch. 4, D-MOV-01). Deliberately does not implement
/// <c>IAuditableEntity</c> - a movement is never updated after creation, so generic
/// CreatedAtUtc/UpdatedAtUtc audit columns would be meaningless; <see cref="PostedBy"/> and
/// <see cref="PostedAtUtc"/> are its only, immutable authorship fields. There is no Update or
/// Remove method - persistence also rejects UPDATE/DELETE at the database level via a trigger
/// (see StockMovementConfiguration and the M3 migration), so append-only holds even outside EF.
/// </summary>
public sealed class StockMovement : Entity
{
    private StockMovement() { }

    public Guid WarehouseId { get; private set; }
    public Guid MaterialId { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid LineId { get; private set; }
    public MovementType MovementType { get; private set; }
    public decimal QuantityDelta { get; private set; }
    public DateTime PostedAtUtc { get; private set; }
    public Guid PostedBy { get; private set; }

    public static Result<StockMovement> Create(
        Guid id,
        Guid warehouseId,
        Guid materialId,
        Guid documentId,
        Guid lineId,
        MovementType movementType,
        decimal quantityDelta,
        Guid postedBy,
        DateTime postedAtUtc)
    {
        if (quantityDelta == 0)
        {
            return Result.Failure<StockMovement>(StockMovementErrors.DeltaMustNotBeZero);
        }

        var movement = new StockMovement
        {
            Id = id,
            WarehouseId = warehouseId,
            MaterialId = materialId,
            DocumentId = documentId,
            LineId = lineId,
            MovementType = movementType,
            QuantityDelta = quantityDelta,
            PostedBy = postedBy,
            PostedAtUtc = postedAtUtc
        };

        movement.Raise(new StockMovementPostedDomainEvent(
            movement.Id,
            warehouseId,
            materialId,
            movementType,
            quantityDelta));

        return movement;
    }
}
