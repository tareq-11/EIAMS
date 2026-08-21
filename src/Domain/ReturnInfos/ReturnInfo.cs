using SharedKernel;

namespace Domain.ReturnInfos;

public sealed class ReturnInfo : Entity, IAuditableEntity
{
    private ReturnInfo() { }

    public Guid OriginalIssueDocumentId { get; private set; }
    public string ReturnReason { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Result<ReturnInfo> Create(Guid documentId, Guid originalIssueDocumentId, string returnReason)
    {
        Result<string> validationResult = Validate(originalIssueDocumentId, returnReason);

        if (validationResult.IsFailure)
        {
            return Result.Failure<ReturnInfo>(validationResult.Error);
        }

        var returnInfo = new ReturnInfo
        {
            Id = documentId,
            OriginalIssueDocumentId = originalIssueDocumentId,
            ReturnReason = validationResult.Value
        };

        returnInfo.Raise(new ReturnInfoCreatedDomainEvent(documentId, originalIssueDocumentId));

        return returnInfo;
    }

    public Result Update(Guid originalIssueDocumentId, string returnReason)
    {
        Result<string> validationResult = Validate(originalIssueDocumentId, returnReason);

        if (validationResult.IsFailure)
        {
            return Result.Failure(validationResult.Error);
        }

        if (OriginalIssueDocumentId == originalIssueDocumentId && ReturnReason == validationResult.Value)
        {
            return Result.Success();
        }

        OriginalIssueDocumentId = originalIssueDocumentId;
        ReturnReason = validationResult.Value;
        Raise(new ReturnInfoUpdatedDomainEvent(Id, originalIssueDocumentId));

        return Result.Success();
    }

    private static Result<string> Validate(Guid originalIssueDocumentId, string returnReason)
    {
        if (originalIssueDocumentId == Guid.Empty)
        {
            return Result.Failure<string>(ReturnInfoErrors.OriginalIssueRequired);
        }

        string normalizedReason = returnReason?.Trim() ?? string.Empty;

        return normalizedReason.Length is 0 or > 200
            ? Result.Failure<string>(ReturnInfoErrors.ReturnReasonInvalid)
            : normalizedReason;
    }
}
