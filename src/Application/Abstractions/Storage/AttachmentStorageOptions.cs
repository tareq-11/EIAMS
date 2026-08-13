namespace Application.Abstractions.Storage;

/// <summary>
/// Validated policy for document attachment uploads (M3-PLAN.md §1.4). Bound from configuration and
/// validated at startup - see Infrastructure's <c>AddInfrastructure</c>.
/// </summary>
public sealed class AttachmentStorageOptions
{
    public const string SectionName = "AttachmentStorage";

    public long MaxFileSizeInBytes { get; init; } = 20 * 1024 * 1024;

    public string[] AllowedMimeTypes { get; init; } =
    [
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/tiff"
    ];

    public bool IsMimeTypeAllowed(string mimeType) =>
        AllowedMimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase);
}
