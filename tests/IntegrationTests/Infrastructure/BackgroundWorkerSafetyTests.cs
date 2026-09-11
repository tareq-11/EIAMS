using Domain.Idempotency;
using Infrastructure.Database;
using Infrastructure.Idempotency;
using Infrastructure.PolymorphicReferences;
using Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace IntegrationTests.Infrastructure;

[Collection(nameof(IntegrationTestCollection))]
public sealed class BackgroundWorkerSafetyTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task FileCleanupWorker_ShouldProcessExactlyConfiguredBatch_AndLeaveOneDueItem()
    {
        FileCleanupWorker worker = GetWorker<FileCleanupWorker>();
        int batchSize = factory.Services.GetRequiredService<IOptions<FileCleanupOptions>>().Value.BatchSize;
        DateTime earliestDueUtc = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Guid[] ids = Enumerable.Range(0, batchSize + 1).Select(_ => Guid.NewGuid()).ToArray();

        try
        {
            for (int index = 0; index < ids.Length; index++)
            {
                await SeedPendingDeletionAsync(ids[index], earliestDueUtc.AddTicks(index));
            }

            FileCleanupWorker.FileCleanupCycleResult result = await worker.RunOnceAsync();

            result.LockUnavailable.ShouldBeFalse();
            result.DeletedCount.ShouldBe(batchSize);
            result.DeferredCount.ShouldBe(0);
            result.ProcessedCount.ShouldBe(batchSize);
            (await PendingDeletionCountAsync(ids)).ShouldBe(1);
        }
        finally
        {
            await DeletePendingDeletionsAsync(ids);
        }
    }

    [Fact]
    public async Task FileCleanupWorker_ShouldSkipCycle_WhenAnotherInstanceOwnsAdvisoryLock()
    {
        FileCleanupWorker worker = GetWorker<FileCleanupWorker>();
        int advisoryLockKey = factory.Services.GetRequiredService<IOptions<FileCleanupOptions>>().Value.AdvisoryLockKey;
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();

        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({advisoryLockKey})");

        FileCleanupWorker.FileCleanupCycleResult result = await worker.RunOnceAsync();

        result.LockUnavailable.ShouldBeTrue();
        result.ProcessedCount.ShouldBe(0);
    }

    [Fact]
    public async Task FileCleanupWorker_ShouldHonorCancellation_BeforeAnyDeletion()
    {
        FileCleanupWorker worker = GetWorker<FileCleanupWorker>();
        var pendingId = Guid.NewGuid();
        try
        {
            await SeedPendingDeletionAsync(pendingId, DateTime.UtcNow.AddMinutes(-1));
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            await Should.ThrowAsync<OperationCanceledException>(() => worker.RunOnceAsync(cancellation.Token));

            (await PendingDeletionExistsAsync(pendingId)).ShouldBeTrue();
        }
        finally
        {
            await DeletePendingDeletionsAsync([pendingId]);
        }
    }

    [Fact]
    public async Task FileCleanupWorker_ShouldKeepFailedDeletionQueued_WithBoundedJitteredBackoff()
    {
        FileCleanupWorker worker = GetWorker<FileCleanupWorker>();
        var pendingId = Guid.NewGuid();
        DateTime beforeCycleUtc = DateTime.UtcNow;

        try
        {
            await SeedPendingDeletionAsync(pendingId, beforeCycleUtc.AddMinutes(-1), storageKey: "invalid-key");

            FileCleanupWorker.FileCleanupCycleResult result = await worker.RunOnceAsync();
            PendingFileDeletion? queued = await FindPendingDeletionAsync(pendingId);

            result.ProcessedCount.ShouldBeGreaterThanOrEqualTo(1);
            result.DeletedCount.ShouldBeGreaterThanOrEqualTo(0);
            result.DeferredCount.ShouldBeGreaterThanOrEqualTo(1);
            FileCleanupWorker.GetCycleOutcome(result).ShouldBe("completed_with_deferred_items");
            queued.ShouldNotBeNull();
            queued.AttemptCount.ShouldBe(1);
            queued.LastError.ShouldNotBeNullOrWhiteSpace();
            queued.NextAttemptAtUtc.ShouldBeGreaterThan(beforeCycleUtc.AddSeconds(1));
            queued.NextAttemptAtUtc.ShouldBeLessThanOrEqualTo(beforeCycleUtc.AddSeconds(3));
        }
        finally
        {
            await DeletePendingDeletionsAsync([pendingId]);
        }
    }

    [Fact]
    public async Task IdempotencyCleanupWorker_ShouldDeleteOnlyExpiredBoundedRecords_AndLeaveValidRecords()
    {
        IdempotencyCleanupWorker worker = GetWorker<IdempotencyCleanupWorker>();
        var expiredFirst = Guid.NewGuid();
        var expiredSecond = Guid.NewGuid();
        var valid = Guid.NewGuid();
        DateTime nowUtc = DateTime.UtcNow;

        try
        {
            await SeedIdempotencyRecordAsync(expiredFirst, nowUtc.AddDays(-2));
            await SeedIdempotencyRecordAsync(expiredSecond, nowUtc.AddDays(-1));
            await SeedIdempotencyRecordAsync(valid, nowUtc.AddDays(1));

            int deleted = await worker.RunOnceAsync(batchSize: 1);

            deleted.ShouldBe(1);
            (await IdempotencyRecordCountAsync([expiredFirst, expiredSecond])).ShouldBe(1);
            (await IdempotencyRecordExistsAsync(valid)).ShouldBeTrue();
        }
        finally
        {
            await DeleteIdempotencyRecordsAsync([expiredFirst, expiredSecond, valid]);
        }
    }

    [Fact]
    public async Task IdempotencyCleanupWorker_ShouldHonorCancellation_WithoutDeletingRecords()
    {
        IdempotencyCleanupWorker worker = GetWorker<IdempotencyCleanupWorker>();
        var expired = Guid.NewGuid();
        try
        {
            await SeedIdempotencyRecordAsync(expired, DateTime.UtcNow.AddDays(-1));
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            await Should.ThrowAsync<OperationCanceledException>(() => worker.RunOnceAsync(1, cancellation.Token));

            (await IdempotencyRecordExistsAsync(expired)).ShouldBeTrue();
        }
        finally
        {
            await DeleteIdempotencyRecordsAsync([expired]);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_001)]
    public async Task IdempotencyCleanupWorker_ShouldRejectUnboundedInternalBatchSizes(int batchSize)
    {
        IdempotencyCleanupWorker worker = GetWorker<IdempotencyCleanupWorker>();

        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => worker.RunOnceAsync(batchSize));
    }

    [Fact]
    public async Task PolymorphicReferenceAuditWorker_ShouldSkip_WhenAnotherInstanceOwnsItsLock()
    {
        PolymorphicReferenceAuditWorker worker = GetWorker<PolymorphicReferenceAuditWorker>();
        int advisoryLockKey = factory.Services.GetRequiredService<IOptions<PolymorphicReferenceAuditOptions>>().Value.AdvisoryLockKey;
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();

        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({advisoryLockKey})");

        string outcome = await worker.RunAuditCycleAsync(10);

        outcome.ShouldBe("skipped_lock_unavailable");
    }

    [Fact]
    public async Task PolymorphicReferenceAuditWorker_ShouldTreatHostCancellationAsNormalShutdown()
    {
        PolymorphicReferenceAuditWorker worker = GetWorker<PolymorphicReferenceAuditWorker>();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        string outcome = await worker.RunAuditCycleAsync(10, cancellation.Token);

        outcome.ShouldBe("cancelled");
    }

    private TWorker GetWorker<TWorker>() where TWorker : class, IHostedService =>
        factory.Services.GetServices<IHostedService>().OfType<TWorker>().Single();

    private async Task SeedPendingDeletionAsync(Guid id, DateTime nextAttemptAtUtc, string? storageKey = null)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Set<PendingFileDeletion>().Add(new PendingFileDeletion
        {
            Id = id,
            StorageKey = storageKey ?? Guid.NewGuid().ToString("N"),
            AttemptCount = 0,
            NextAttemptAtUtc = nextAttemptAtUtc,
            CreatedAtUtc = nextAttemptAtUtc,
        });
        await context.SaveChangesAsync();
    }

    private async Task<bool> PendingDeletionExistsAsync(Guid id)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Set<PendingFileDeletion>().AnyAsync(item => item.Id == id);
    }

    private async Task<int> PendingDeletionCountAsync(IReadOnlyCollection<Guid> ids)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Set<PendingFileDeletion>().CountAsync(item => ids.Contains(item.Id));
    }

    private async Task DeletePendingDeletionsAsync(IReadOnlyCollection<Guid> ids)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Set<PendingFileDeletion>()
            .Where(item => ids.Contains(item.Id))
            .ExecuteDeleteAsync();
    }

    private async Task<PendingFileDeletion?> FindPendingDeletionAsync(Guid id)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Set<PendingFileDeletion>().SingleOrDefaultAsync(item => item.Id == id);
    }

    private async Task SeedIdempotencyRecordAsync(Guid key, DateTime expiresAtUtc)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Guid actorUserId = await context.Users
            .Where(user => user.Email == IntegrationTestWebAppFactory.AdministratorEmail)
            .Select(user => user.Id)
            .SingleAsync();
        context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            key,
            "background-worker-test",
            actorUserId,
            new string('A', 64),
            "{}",
            expiresAtUtc.AddHours(-1),
            expiresAtUtc));
        await context.SaveChangesAsync();
    }

    private async Task<bool> IdempotencyRecordExistsAsync(Guid key)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.IdempotencyRecords.AnyAsync(record => record.Key == key);
    }

    private async Task<int> IdempotencyRecordCountAsync(IReadOnlyCollection<Guid> keys)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.IdempotencyRecords.CountAsync(record => keys.Contains(record.Key));
    }

    private async Task DeleteIdempotencyRecordsAsync(IReadOnlyCollection<Guid> keys)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.IdempotencyRecords
            .Where(record => keys.Contains(record.Key))
            .ExecuteDeleteAsync();
    }
}
