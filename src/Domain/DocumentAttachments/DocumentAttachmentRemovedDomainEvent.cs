using Domain.Common;
using SharedKernel;

namespace Domain.DocumentAttachments;

public sealed record DocumentAttachmentRemovedDomainEvent(Guid AttachmentId, Guid DocumentId, AttachmentType AttachmentType)
    : IDomainEvent;
