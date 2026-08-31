namespace Application.DocumentAttachments.GetList;

public sealed record DocumentAttachmentResponse(
    Guid Id,
    string AttachmentType,
    string OriginalFilename,
    string MimeType,
    long FileSize,
    string Checksum,
    Guid UploadedBy,
    DateTime UploadedAtUtc,
    bool IsActive,
    DateTime? ArchivedAtUtc,
    Guid? ArchivedBy,
    Guid? ReplacesAttachmentId,
    Guid? ReplacedByAttachmentId);
