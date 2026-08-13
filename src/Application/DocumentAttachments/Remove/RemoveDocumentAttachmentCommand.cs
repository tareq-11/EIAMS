using Application.Abstractions.Messaging;

namespace Application.DocumentAttachments.Remove;

public sealed record RemoveDocumentAttachmentCommand(Guid DocumentId, Guid AttachmentId, int ExpectedRowVersion)
    : ICommand;
