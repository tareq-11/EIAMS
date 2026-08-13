namespace Infrastructure.Storage;

/// <summary>Controls polling and retry behavior for failed attachment deletions.</summary>
public sealed class FileCleanupOptions
{
    /// <summary>The configuration section containing cleanup settings.</summary>
    public const string SectionName = "AttachmentStorage:Cleanup";

    /// <summary>Gets the delay between durable-queue polling cycles.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets the maximum number of pending deletions processed in one cycle.</summary>
    public int BatchSize { get; init; } = 25;

    /// <summary>Gets the upper bound for exponential retry backoff.</summary>
    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromHours(1);
}
