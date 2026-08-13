using SharedKernel;

namespace Domain.TransferInfos;

/// <summary>The destination detail for an atomic Transfer <c>WarehouseDocument</c>.</summary>
public sealed class TransferInfo : Entity, IAuditableEntity
{
    private TransferInfo() { }

    public Guid DestinationWarehouseId { get; private set; }
    public string TransferReason { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Result<TransferInfo> Create(Guid documentId, Guid destinationWarehouseId, string transferReason)
    {
        Result<string> validationResult = Validate(destinationWarehouseId, transferReason);

        if (validationResult.IsFailure)
        {
            return Result.Failure<TransferInfo>(validationResult.Error);
        }

        var transferInfo = new TransferInfo
        {
            Id = documentId,
            DestinationWarehouseId = destinationWarehouseId,
            TransferReason = validationResult.Value
        };

        transferInfo.Raise(new TransferInfoCreatedDomainEvent(documentId, destinationWarehouseId));

        return transferInfo;
    }

    public Result Update(Guid destinationWarehouseId, string transferReason)
    {
        Result<string> validationResult = Validate(destinationWarehouseId, transferReason);

        if (validationResult.IsFailure)
        {
            return Result.Failure(validationResult.Error);
        }

        if (DestinationWarehouseId == destinationWarehouseId && TransferReason == validationResult.Value)
        {
            return Result.Success();
        }

        DestinationWarehouseId = destinationWarehouseId;
        TransferReason = validationResult.Value;

        Raise(new TransferInfoUpdatedDomainEvent(Id, destinationWarehouseId));

        return Result.Success();
    }

    private static Result<string> Validate(Guid destinationWarehouseId, string transferReason)
    {
        if (destinationWarehouseId == Guid.Empty)
        {
            return Result.Failure<string>(TransferInfoErrors.DestinationRequired);
        }

        string normalizedReason = transferReason?.Trim() ?? string.Empty;

        if (normalizedReason.Length is 0 or > 200)
        {
            return Result.Failure<string>(TransferInfoErrors.TransferReasonInvalid);
        }

        return normalizedReason;
    }
}
