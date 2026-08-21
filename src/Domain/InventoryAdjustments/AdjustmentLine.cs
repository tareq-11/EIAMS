using SharedKernel;

namespace Domain.InventoryAdjustments;

public sealed class AdjustmentLine : Entity, IAuditableEntity
{
    private const decimal MaximumDifference = 999_999_999_999_999.999m;
    private AdjustmentLine() { }

    public Guid AdjustmentId { get; private set; }
    public decimal Difference { get; private set; }
    public string Reason { get; private set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Result<AdjustmentLine> Create(
        Guid documentLineId,
        Guid adjustmentId,
        decimal difference,
        string reason,
        bool allowZero = false)
    {
        if (documentLineId == Guid.Empty || adjustmentId == Guid.Empty)
        {
            return Result.Failure<AdjustmentLine>(AdjustmentLineErrors.IdentityRequired);
        }

        Result validation = Validate(difference, reason, allowZero);
        if (validation.IsFailure)
        {
            return Result.Failure<AdjustmentLine>(validation.Error);
        }

        string normalized = reason.Trim();

        var line = new AdjustmentLine
        {
            Id = documentLineId,
            AdjustmentId = adjustmentId,
            Difference = difference,
            Reason = normalized
        };
        line.Raise(new AdjustmentLineCreatedDomainEvent(documentLineId, adjustmentId));
        return line;
    }

    public Result Update(decimal difference, string reason)
    {
        Result validation = Validate(difference, reason, allowZero: false);
        if (validation.IsFailure)
        {
            return validation;
        }

        Difference = difference;
        Reason = reason.Trim();
        Raise(new AdjustmentLineUpdatedDomainEvent(Id, AdjustmentId));
        return Result.Success();
    }

    public void MarkAsRemoved() => Raise(new AdjustmentLineRemovedDomainEvent(Id, AdjustmentId));

    private static Result Validate(decimal difference, string reason, bool allowZero)
    {
        if (difference < -MaximumDifference || difference > MaximumDifference ||
            decimal.Round(difference, 3) != difference)
        {
            return Result.Failure(AdjustmentLineErrors.DifferenceInvalid);
        }

        if (!allowZero && difference == 0)
        {
            return Result.Failure(AdjustmentLineErrors.ZeroDifference);
        }

        string normalized = reason?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return Result.Failure(AdjustmentLineErrors.ReasonRequired);
        }

        return normalized.Length > 200
            ? Result.Failure(AdjustmentLineErrors.ReasonTooLong)
            : Result.Success();
    }
}
