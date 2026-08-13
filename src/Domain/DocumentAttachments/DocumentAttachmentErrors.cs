using SharedKernel;

namespace Domain.DocumentAttachments;

public static class DocumentAttachmentErrors
{
    public static Error NotFound(Guid attachmentId) => Error.NotFound(
        "DocumentAttachments.NotFound",
        $"The document attachment with the Id = '{attachmentId}' was not found",
        new { attachment_id = attachmentId });

    public static Error SignedOriginalAlreadyExists(Guid documentId) => Error.Conflict(
        "DocumentAttachments.SignedOriginalAlreadyExists",
        $"The document with the Id = '{documentId}' already has a SignedOriginal attachment; remove it before uploading a new one.",
        new { document_id = documentId });

    public static readonly Error FileEmpty = Error.Problem(
        "DocumentAttachments.FileEmpty",
        "The uploaded file is empty.");

    public static Error FileTooLarge(long maxSizeInBytes) => Error.Problem(
        "DocumentAttachments.FileTooLarge",
        $"The uploaded file exceeds the maximum allowed size of {maxSizeInBytes} bytes.",
        new { max_size_in_bytes = maxSizeInBytes });

    public static Error MimeTypeNotAllowed(string mimeType) => Error.Problem(
        "DocumentAttachments.MimeTypeNotAllowed",
        $"The MIME type '{mimeType}' is not allowed for document attachments.",
        new { mime_type = mimeType });

    public static readonly Error NotEditable = Error.Problem(
        "DocumentAttachments.NotEditable",
        "Attachments can only be uploaded to or removed from a Draft document.");

    public static readonly Error StorageFailure = Error.Failure(
        "DocumentAttachments.StorageFailure",
        "The file could not be stored.");

    public static Error ContentNotFound(string storageKey) => Error.NotFound(
        "DocumentAttachments.ContentNotFound",
        "The attachment's stored file content could not be found.",
        new { storage_key = storageKey });
}
