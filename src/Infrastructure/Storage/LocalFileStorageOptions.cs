namespace Infrastructure.Storage;

/// <summary>
/// Where the local-filesystem <see cref="IFileStorage"/> implementation writes attachment content.
/// Development-only - production deployments swap in a cloud-storage implementation behind the same
/// <c>IFileStorage</c> abstraction without touching document workflows (M3-PLAN.md §8).
/// </summary>
public sealed class LocalFileStorageOptions
{
    public const string SectionName = "AttachmentStorage:Local";

    /// <summary>
    /// Path to the storage root, relative to the content root unless rooted. Kept outside the
    /// source tree and never served as static content.
    /// </summary>
    public string RootPath { get; init; } = "../../attachment-storage";
}
