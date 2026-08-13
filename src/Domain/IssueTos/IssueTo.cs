using Domain.Common;
using SharedKernel;

namespace Domain.IssueTos;

/// <summary>The type-specific recipient detail for an Issue <c>WarehouseDocument</c>.</summary>
public sealed class IssueTo : Entity, IAuditableEntity
{
    private IssueTo() { }

    public PartyType RecipientType { get; private set; }
    public Guid RecipientId { get; private set; }
    public string IssueReason { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Result<IssueTo> Create(
        Guid documentId,
        PartyType recipientType,
        Guid recipientId,
        string issueReason)
    {
        Result<string> validationResult = Validate(recipientType, recipientId, issueReason);

        if (validationResult.IsFailure)
        {
            return Result.Failure<IssueTo>(validationResult.Error);
        }

        var issueTo = new IssueTo
        {
            Id = documentId,
            RecipientType = recipientType,
            RecipientId = recipientId,
            IssueReason = validationResult.Value
        };

        issueTo.Raise(new IssueToCreatedDomainEvent(documentId, recipientType, recipientId));

        return issueTo;
    }

    public Result Update(PartyType recipientType, Guid recipientId, string issueReason)
    {
        Result<string> validationResult = Validate(recipientType, recipientId, issueReason);

        if (validationResult.IsFailure)
        {
            return Result.Failure(validationResult.Error);
        }

        if (RecipientType == recipientType &&
            RecipientId == recipientId &&
            IssueReason == validationResult.Value)
        {
            return Result.Success();
        }

        RecipientType = recipientType;
        RecipientId = recipientId;
        IssueReason = validationResult.Value;

        Raise(new IssueToUpdatedDomainEvent(Id, recipientType, recipientId));

        return Result.Success();
    }

    private static Result<string> Validate(PartyType recipientType, Guid recipientId, string issueReason)
    {
        if (!Enum.IsDefined(recipientType))
        {
            return Result.Failure<string>(IssueToErrors.RecipientTypeInvalid);
        }

        if (recipientId == Guid.Empty)
        {
            return Result.Failure<string>(IssueToErrors.RecipientRequired);
        }

        string normalizedReason = issueReason?.Trim() ?? string.Empty;

        if (normalizedReason.Length is 0 or > 200)
        {
            return Result.Failure<string>(IssueToErrors.IssueReasonInvalid);
        }

        return normalizedReason;
    }
}
