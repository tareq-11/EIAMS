using Application.Abstractions.Storage;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Infrastructure.Storage;

internal sealed class AttachmentFileCleanup(
    IFileStorage fileStorage,
    IServiceScopeFactory scopeFactory,
    IDateTimeProvider dateTimeProvider,
    ILogger<AttachmentFileCleanup> logger) : IAttachmentFileCleanup
{
    public async Task DeleteOrEnqueueAsync(string storageKey, CancellationToken cancellationToken)
    {
        Result deletionResult = await fileStorage.DeleteAsync(storageKey, cancellationToken);

        if (deletionResult.IsSuccess)
        {
            return;
        }

        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            DateTime nowUtc = dateTimeProvider.UtcNow;
            string lastError = PendingFileDeletion.NormalizeError(deletionResult.Error.Description);

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO public.pending_file_deletion
                     (id, storage_key, attempt_count, next_attempt_at_utc, created_at_utc, last_error)
                 VALUES
                     ({Guid.NewGuid()}, {storageKey}, 0, {nowUtc}, {nowUtc}, {lastError})
                 ON CONFLICT (storage_key) DO UPDATE
                 SET next_attempt_at_utc = LEAST(pending_file_deletion.next_attempt_at_utc, EXCLUDED.next_attempt_at_utc),
                     last_error = EXCLUDED.last_error
                 """,
                cancellationToken);

            logger.LogWarning(
                "Queued attachment file {StorageKey} for background deletion",
                storageKey);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The primary database mutation has already succeeded (or a failed upload is being
            // compensated), so cleanup cannot safely turn that operation into an API failure.
            logger.LogCritical(
                exception,
                "Failed to queue attachment file {StorageKey} for deletion; manual cleanup may be required",
                storageKey);
        }
    }
}
