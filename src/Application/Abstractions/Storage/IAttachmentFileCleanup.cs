namespace Application.Abstractions.Storage;

/// <summary>
/// Deletes an attachment file immediately when possible and durably schedules a retry when the
/// storage provider is temporarily unavailable.
/// </summary>
public interface IAttachmentFileCleanup
{
    /// <summary>
    /// Deletes the file identified by <paramref name="storageKey"/> or records it for background
    /// cleanup. This operation is best-effort and does not undo the caller's completed database
    /// operation.
    /// </summary>
    Task DeleteOrEnqueueAsync(string storageKey, CancellationToken cancellationToken);
}
