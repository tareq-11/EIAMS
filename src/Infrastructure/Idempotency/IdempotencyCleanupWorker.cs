using Infrastructure.Database;
using Infrastructure.BackgroundWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Infrastructure.Idempotency;

internal sealed class IdempotencyCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<IdempotencyCleanupOptions> options,
    ILogger<IdempotencyCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IdempotencyCleanupOptions settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        try
        {
            await Task.Delay(settings.InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        using var timer = new PeriodicTimer(settings.Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            long startedTimestamp = BackgroundWorkerMetrics.Start();
            try
            {
                int deleted = await RunOnceAsync(settings.BatchSize, stoppingToken);
                BackgroundWorkerMetrics.Record("idempotency_cleanup", "succeeded", startedTimestamp, deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                BackgroundWorkerMetrics.Record("idempotency_cleanup", "cancelled", startedTimestamp);
                return;
            }
            catch (Exception exception)
            {
                BackgroundWorkerMetrics.Record("idempotency_cleanup", "failed", startedTimestamp);
                logger.LogError(exception, "Failed to clean expired idempotency records; the bounded cycle will be retried later");
            }
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    return;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    /// <summary>Runs one bounded, SKIP LOCKED cleanup batch.</summary>
    internal async Task<int> RunOnceAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(batchSize, 10_000);

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IDateTimeProvider dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        DateTime nowUtc = dateTimeProvider.UtcNow;

        int deleted = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             WITH expired AS (
                 SELECT key
                 FROM public.idempotency_records
                 WHERE expires_at_utc <= {nowUtc}
                 ORDER BY expires_at_utc, key
                 LIMIT {batchSize}
                 FOR UPDATE SKIP LOCKED
             )
             DELETE FROM public.idempotency_records AS records
             USING expired
             WHERE records.key = expired.key
             """,
            cancellationToken);

        if (deleted > 0)
        {
            logger.LogInformation("Deleted {RecordCount} expired idempotency records", deleted);
        }

        return deleted;
    }
}
