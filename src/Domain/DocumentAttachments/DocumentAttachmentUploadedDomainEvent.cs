using Domain.Common;
using SharedKernel;

namespace Domain.DocumentAttachments;

public sealed record DocumentAttachmentUploadedDomainEvent(Guid AttachmentId, Guid DocumentId, AttachmentType AttachmentType)
    : IDomainEvent;
