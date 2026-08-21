namespace Application.Custodies.GetPending;

public sealed record PendingCustodyResponse(
    Guid CustodyId,
    Guid AssetId,
    string AssetNumber,
    Guid MaterialId,
    string HolderType,
    Guid HolderId,
    Guid IssueDocumentId,
    DateTime FromUtc,
    int RowVersion);
