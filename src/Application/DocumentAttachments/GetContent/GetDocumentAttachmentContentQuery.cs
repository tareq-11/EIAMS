using Application.Abstractions.Messaging;

namespace Application.DocumentAttachments.GetContent;

public sealed record GetDocumentAttachmentContentQuery(Guid DocumentId, Guid AttachmentId)
    : IQuery<DocumentAttachmentContentResponse>;
