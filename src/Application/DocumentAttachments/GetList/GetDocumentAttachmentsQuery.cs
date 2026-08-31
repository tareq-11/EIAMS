using Application.Abstractions.Messaging;

namespace Application.DocumentAttachments.GetList;

public sealed record GetDocumentAttachmentsQuery(Guid DocumentId, bool IncludeArchived)
    : IQuery<List<DocumentAttachmentResponse>>;
