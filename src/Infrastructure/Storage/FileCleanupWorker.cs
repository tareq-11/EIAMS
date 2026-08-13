using Application.Abstractions.Storage;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
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
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Attachment file cleanup cycle failed");
            }

            await Task.Delay(options.Value.PollInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IFileStorage fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        DateTime nowUtc = dateTimeProvider.UtcNow;

        List<PendingFileDeletion> pendingItems = await dbContext.Set<PendingFileDeletion>()
            .Where(item => item.NextAttemptAtUtc <= nowUtc)
            .OrderBy(item => item.NextAttemptAtUtc)
            .ThenBy(item => item.CreatedAtUtc)
            .Take(options.Value.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (PendingFileDeletion item in pendingItems)
        {
            Result deletionResult = await fileStorage.DeleteAsync(item.StorageKey, cancellationToken);

            if (deletionResult.IsSuccess)
            {
                dbContext.Remove(item);
                logger.LogInformation(
                    "Deleted queued attachment file {StorageKey} after {AttemptCount} failed attempts",
                    item.StorageKey,
                    item.AttemptCount);
            }
            else
            {
                item.AttemptCount++;
                item.LastError = PendingFileDeletion.NormalizeError(deletionResult.Error.Description);
                item.NextAttemptAtUtc = nowUtc.Add(CalculateRetryDelay(item.AttemptCount));
            }
        }

        if (pendingItems.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private TimeSpan CalculateRetryDelay(int attemptCount)
    {
        int cappedExponent = Math.Min(attemptCount, 10);
        var delay = TimeSpan.FromSeconds(Math.Pow(2, cappedExponent));

        return delay <= options.Value.MaxRetryDelay ? delay : options.Value.MaxRetryDelay;
    }
}
