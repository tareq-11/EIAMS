using SharedKernel;

namespace Domain.DocumentAttachments;

public sealed record DocumentAttachmentArchivedDomainEvent(
    Guid AttachmentId,
    Guid DocumentId,
    Guid ReplacementAttachmentId,
    Guid ArchivedBy,
    DateTime ArchivedAtUtc) : IDomainEvent;
