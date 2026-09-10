using Application.Abstractions.Storage;
using Infrastructure.BackgroundWork;
using Infrastructure.Database;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Infrastructure.Storage;

internal sealed class FileCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<FileCleanupOptions> options,
    IDateTimeProvider dateTimeProvider,
    ILogger<FileCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            long startedTimestamp = BackgroundWorkerMetrics.Start();
            try
            {
                FileCleanupCycleResult result = await RunOnceAsync(stoppingToken);
                BackgroundWorkerMetrics.Record(
                    "file_cleanup",
                    GetCycleOutcome(result),
                    startedTimestamp,
                    result.ProcessedCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                BackgroundWorkerMetrics.Record("file_cleanup", "cancelled", startedTimestamp);
                break;
            }
            catch (Exception exception)
            {
                BackgroundWorkerMetrics.Record("file_cleanup", "failed", startedTimestamp);
                logger.LogError(exception, "Attachment file cleanup cycle failed; queued files will be retried on a later cycle");
            }

            try
            {
                await Task.Delay(options.Value.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <summary>Runs one bounded cleanup cycle. Intended for host orchestration and integration tests.</summary>
    internal async Task<FileCleanupCycleResult> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IFileStorage fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        DateTime nowUtc = dateTimeProvider.UtcNow;
        FileCleanupOptions settings = options.Value;

        // The durable queue has no claim/lease column. Keep the transaction-scoped advisory lock
        // for the bounded cycle so concurrent app instances cannot delete the same object twice.
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        bool lockAcquired = await dbContext.Database
            .SqlQueryRaw<bool>("SELECT pg_try_advisory_xact_lock({0}) AS \"Value\"", settings.AdvisoryLockKey)
            .SingleAsync(cancellationToken);

        if (!lockAcquired)
        {
            return new FileCleanupCycleResult(0, 0, true);
        }

        int deletedCount = 0;
        int deferredCount = 0;

        List<PendingFileDeletion> pendingItems = await dbContext.Set<PendingFileDeletion>()
            .Where(item => item.NextAttemptAtUtc <= nowUtc)
            .OrderBy(item => item.NextAttemptAtUtc)
            .ThenBy(item => item.CreatedAtUtc)
            .Take(settings.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (PendingFileDeletion item in pendingItems)
        {
            Result deletionResult = await fileStorage.DeleteAsync(item.StorageKey, cancellationToken);

            if (deletionResult.IsSuccess)
            {
                dbContext.Remove(item);
                deletedCount++;
                logger.LogInformation(
                    "Deleted one queued attachment file after {AttemptCount} failed attempts",
                    item.AttemptCount);
            }
            else
            {
                item.AttemptCount++;
                item.LastError = PendingFileDeletion.NormalizeError(deletionResult.Error.Description);
                item.NextAttemptAtUtc = nowUtc.Add(CalculateRetryDelay(item.AttemptCount));
                deferredCount++;
                logger.LogWarning(
                    "Attachment file cleanup deferred one queued item after attempt {AttemptCount}",
                    item.AttemptCount);
            }
        }

        if (pendingItems.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new FileCleanupCycleResult(deletedCount, deferredCount, false);
    }

    private TimeSpan CalculateRetryDelay(int attemptCount)
    {
        int cappedExponent = Math.Min(attemptCount, 10);
        var delay = TimeSpan.FromSeconds(Math.Pow(2, cappedExponent));

        // A small bounded jitter prevents replicas from retrying all failed files in lockstep.
        int jitterPercent = RandomNumberGenerator.GetInt32(80, 121);
        delay = TimeSpan.FromTicks(delay.Ticks * jitterPercent / 100);

        return delay <= options.Value.MaxRetryDelay ? delay : options.Value.MaxRetryDelay;
    }

    internal static string GetCycleOutcome(FileCleanupCycleResult result) => result switch
    {
        { LockUnavailable: true } => "skipped_lock_unavailable",
        { DeferredCount: > 0 } => "completed_with_deferred_items",
        _ => "succeeded",
    };

    internal sealed record FileCleanupCycleResult(int DeletedCount, int DeferredCount, bool LockUnavailable)
    {
        internal int ProcessedCount => DeletedCount + DeferredCount;
    }
}
