using Domain.Common;
using SharedKernel;

namespace Domain.DocumentSequences;

public static class DocumentSequenceErrors
{
    public static Error SiteNotFound(Guid siteId) => Error.NotFound(
        "DocumentSequences.SiteNotFound",
        $"The site with the Id = '{siteId}' was not found",
        new { site_id = siteId });

    public static Error SiteInactive(Guid siteId) => Error.Problem(
        "DocumentSequences.SiteInactive",
        $"The site with the Id = '{siteId}' is inactive and cannot receive numbered documents.",
        new { site_id = siteId });

    public static Error ReferenceNumberTooLong(int maxLength) => Error.Problem(
        "DocumentSequences.ReferenceNumberTooLong",
        $"The generated reference number exceeds the maximum allowed length ({maxLength} characters).",
        new { max_length = maxLength });

    public static Error SiteCodeContainsSeparator(string siteCode, string separator) => Error.Problem(
        "DocumentSequences.SiteCodeContainsSeparator",
        $"The site code '{siteCode}' contains the configured reference-number separator '{separator}'.",
        new { site_code = siteCode, separator });

    public static Error InvalidDocumentType(DocumentType documentType) => Error.Problem(
        "DocumentSequences.InvalidDocumentType",
        $"The document type value '{documentType}' is not supported for reference numbering.",
        new { document_type = documentType.ToString() });
}
