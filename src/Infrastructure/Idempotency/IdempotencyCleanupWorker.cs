using Infrastructure.Database;
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

        await Task.Delay(settings.InitialDelay, stoppingToken);

        using var timer = new PeriodicTimer(settings.Interval);
        do
        {
            try
            {
                await DeleteExpiredBatchAsync(settings.BatchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to clean expired idempotency records");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DeleteExpiredBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
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
    }
}
