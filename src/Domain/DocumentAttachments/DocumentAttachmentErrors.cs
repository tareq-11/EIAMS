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
        $"The document with the Id = '{documentId}' already has an active SignedOriginal attachment.",
        new { document_id = documentId });

    public static Error AlreadyArchived(Guid attachmentId) => Error.Conflict(
        "DocumentAttachments.AlreadyArchived",
        $"The document attachment with the Id = '{attachmentId}' is already archived.",
        new { attachment_id = attachmentId });

    public static Error OnlySignedOriginalCanBeArchived(Guid attachmentId) => Error.Problem(
        "DocumentAttachments.OnlySignedOriginalCanBeArchived",
        "Only a SignedOriginal attachment can be archived during replacement.",
        new { attachment_id = attachmentId });

    public static Error InvalidReplacement(Guid attachmentId) => Error.Problem(
        "DocumentAttachments.InvalidReplacement",
        "A SignedOriginal attachment cannot replace itself.",
        new { attachment_id = attachmentId });

    public static Error ArchivedCannotBeRemoved(Guid attachmentId) => Error.Conflict(
        "DocumentAttachments.ArchivedCannotBeRemoved",
        "An archived SignedOriginal is immutable and cannot be removed.",
        new { attachment_id = attachmentId });

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

    public static readonly Error FileSignatureMismatch = Error.Problem(
        "DocumentAttachments.FileSignatureMismatch",
        "The file content does not match its declared MIME type.");

    public static readonly Error MalwareScanRejected = Error.Problem(
        "DocumentAttachments.MalwareScanRejected",
        "The attachment could not pass the required security scan.");

    public static readonly Error MalwareScannerUnavailable = Error.Unavailable(
        "DocumentAttachments.MalwareScannerUnavailable",
        "The attachment security scan is temporarily unavailable.");

    public static readonly Error MalwareScanRequired = Error.Unavailable(
        "DocumentAttachments.MalwareScanRequired",
        "The attachment is unavailable until it has passed the required security scan.");

    public static readonly Error NotEditable = Error.Problem(
        "DocumentAttachments.NotEditable",
        "Attachments can only be uploaded to or removed from a Draft document.");

    public static readonly Error StorageFailure = Error.Failure(
        "DocumentAttachments.StorageFailure",
        "The file could not be stored.");

    public static readonly Error ContentNotFound = Error.NotFound(
        "DocumentAttachments.ContentNotFound",
        "The attachment's stored file content could not be found.");
}
