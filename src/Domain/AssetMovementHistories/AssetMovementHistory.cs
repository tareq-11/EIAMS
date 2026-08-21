using Domain.Common;
using SharedKernel;

namespace Domain.AssetMovementHistories;

public sealed class AssetMovementHistory : Entity, IAuditableEntity
{
    private AssetMovementHistory() { }

    public Guid AssetId { get; private set; }
    public Guid DocumentId { get; private set; }
    public AssetMovementType MovementType { get; private set; }
    public DateTime MovedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Result<AssetMovementHistory> Create(
        Guid id,
        Guid assetId,
        Guid documentId,
        AssetMovementType movementType,
        DateTime movedAtUtc)
    {
        if (id == Guid.Empty || assetId == Guid.Empty || documentId == Guid.Empty)
        {
            return Result.Failure<AssetMovementHistory>(AssetMovementHistoryErrors.IdentityRequired);
        }

        if (!Enum.IsDefined(movementType))
        {
            return Result.Failure<AssetMovementHistory>(AssetMovementHistoryErrors.MovementTypeInvalid);
        }

        var history = new AssetMovementHistory
        {
            Id = id,
            AssetId = assetId,
            DocumentId = documentId,
            MovementType = movementType,
            MovedAtUtc = movedAtUtc
        };

        history.Raise(new AssetMovementHistoryAppendedDomainEvent(id, assetId, documentId, movementType));

        return history;
    }
}
