using Application.Abstractions.Storage;
using Application.DocumentAttachments.GetList;

namespace Application.DocumentAttachments;

/// <summary>
/// Authoritative result of an attachment mutation. Storage keys and other storage internals are
/// deliberately omitted; callers use the document row version for their next conditional write.
/// </summary>
public sealed record AttachmentMutationResponse(
    Guid AttachmentId,
    bool Removed,
    int DocumentRowVersion,
    string MalwareScanPolicy,
    bool MalwareScanClean,
    DocumentAttachmentResponse Attachment);
