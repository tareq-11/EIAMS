namespace Application.Custodies.GetTimeline;

public sealed record AssetCustodyTimelineResponse(
    Guid CustodyId,
    Guid AssetId,
    string HolderType,
    Guid HolderId,
    string CustodyKind,
    Guid IssueDocumentId,
    Guid? ReturnDocumentId,
    string Status,
    DateTime FromUtc,
    DateTime? ToUtc,
    int RowVersion);
