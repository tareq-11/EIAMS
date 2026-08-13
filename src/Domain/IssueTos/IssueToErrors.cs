using Domain.Common;
using SharedKernel;

namespace Domain.IssueTos;

public static class IssueToErrors
{
    public static Error Required(Guid documentId) => Error.Problem(
        "IssueTos.Required",
        "Issue recipient information is required before the document can be submitted or posted.",
        new { document_id = documentId });

    public static Error WrongDocumentType(Guid documentId) => Error.Problem(
        "IssueTos.WrongDocumentType",
        "Issue recipient information can only be attached to an Issue document.",
        new { document_id = documentId });

    public static Error RecipientNotFound(PartyType recipientType, Guid recipientId) => Error.Problem(
        "IssueTos.RecipientNotFound",
        "The selected issue recipient does not exist.",
        new { recipient_type = recipientType.ToString(), recipient_id = recipientId });

    public static Error RecipientInactive(PartyType recipientType, Guid recipientId) => Error.Problem(
        "IssueTos.RecipientInactive",
        "The selected issue recipient is inactive.",
        new { recipient_type = recipientType.ToString(), recipient_id = recipientId });

    public static readonly Error ExternalRecipientNotSupported = Error.Problem(
        "IssueTos.ExternalRecipientNotSupported",
        "External recipients are not supported until an active external-party master is available.");

    public static readonly Error RecipientTypeInvalid = Error.Problem(
        "IssueTos.RecipientTypeInvalid",
        "RecipientType must be a known value.");

    public static readonly Error RecipientRequired = Error.Problem(
        "IssueTos.RecipientRequired",
        "RecipientId is required.");

    public static readonly Error IssueReasonInvalid = Error.Problem(
        "IssueTos.IssueReasonInvalid",
        "Issue reason is required and must not exceed 200 characters.");

    public static Error AssetLinesNotSupported(Guid documentId) => Error.Problem(
        "IssueTos.AssetLinesNotSupported",
        "Asset-tracked issue lines are supported when custody tracking is introduced in M6.",
        new { document_id = documentId });
}
