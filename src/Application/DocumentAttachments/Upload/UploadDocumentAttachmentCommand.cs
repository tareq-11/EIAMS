using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.DocumentAttachments.Upload;

public sealed record UploadDocumentAttachmentCommand(
    Guid DocumentId,
    AttachmentType AttachmentType,
    Stream Content,
    string OriginalFilename,
    string MimeType,
    long ContentLength,
    int ExpectedRowVersion) : ICommand<Guid>;
