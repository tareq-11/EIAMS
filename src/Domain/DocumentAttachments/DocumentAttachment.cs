using Domain.Common;
using SharedKernel;

namespace Domain.DocumentAttachments;

/// <summary>
/// File metadata for one attachment on a WarehouseDocument (not the file bytes themselves - those
/// live behind <c>IFileStorage</c>, addressed by <see cref="StorageKey"/>). A document has at most
/// one active SignedOriginal; that uniqueness is enforced at the database level (partial unique
/// index), not here. Replacing an attachment is remove-then-upload, not an in-place update, so this
/// entity exposes no Update method - only Create and MarkAsRemoved, both raising events so a later
/// audit-log module can instrument uploads/removals (M8).
/// </summary>
public sealed class DocumentAttachment : Entity, IAuditableEntity
{
    private DocumentAttachment() { }

    public Guid DocumentId { get; private set; }
    public AttachmentType AttachmentType { get; private set; }
    public string StorageKey { get; private set; }
    public string OriginalFilename { get; private set; }
    public string MimeType { get; private set; }
    public long FileSize { get; private set; }
    public string Checksum { get; private set; }
    public Guid UploadedBy { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static DocumentAttachment Create(
        Guid id,
        Guid documentId,
        AttachmentType attachmentType,
        string storageKey,
        string originalFilename,
        string mimeType,
        long fileSize,
        string checksum,
        Guid uploadedBy,
        DateTime uploadedAtUtc)
    {
        var attachment = new DocumentAttachment
        {
            Id = id,
            DocumentId = documentId,
            AttachmentType = attachmentType,
            StorageKey = storageKey,
            OriginalFilename = originalFilename,
            MimeType = mimeType,
            FileSize = fileSize,
            Checksum = checksum,
            UploadedBy = uploadedBy,
            UploadedAtUtc = uploadedAtUtc
        };

        attachment.Raise(new DocumentAttachmentUploadedDomainEvent(attachment.Id, documentId, attachmentType));

        return attachment;
    }

    public void MarkAsRemoved()
    {
        Raise(new DocumentAttachmentRemovedDomainEvent(Id, DocumentId, AttachmentType));
    }
}
