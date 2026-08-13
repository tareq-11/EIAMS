namespace Infrastructure.Storage;

/// <summary>A durable work item for an attachment file that could not be deleted immediately.</summary>
internal sealed class PendingFileDeletion
{
    public const int MaxLastErrorLength = 2_000;

    public Guid Id { get; set; }
    public required string StorageKey { get; set; }
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? LastError { get; set; }

    public static string NormalizeError(string error) =>
        error[..Math.Min(error.Length, MaxLastErrorLength)];
}
