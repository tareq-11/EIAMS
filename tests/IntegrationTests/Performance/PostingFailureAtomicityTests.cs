using Application.Abstractions.Data;
using Domain.Organizations;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SharedKernel;

namespace IntegrationTests.Performance;

/// <summary>
/// Exercises the transaction boundary used by posting and reversal against real PostgreSQL.
/// The tests make the failure happen after a SQL write has been flushed, so a passing test proves
/// rollback rather than merely proving that validation stopped before a write was attempted.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public sealed class PostingFailureAtomicityTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task CancellationAfterFlushedWrite_ShouldRollbackAllTransactionEffects()
    {
        var dispatcher = new RecordingDomainEventsDispatcher();
        await using ApplicationDbContext context = CreateContext(dispatcher);
        var transaction = new EfApplicationTransaction(context);
        var markerId = Guid.NewGuid();
        var writeFlushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        Task<Result> execution = transaction.ExecuteAsync(
            async cancellationToken =>
            {
                context.Organizations.Add(CreateOrganization(markerId, "CANCEL"));
                await context.SaveChangesAsync(cancellationToken);
                writeFlushed.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return Result.Success();
            },
            cancellation.Token);

        await writeFlushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => execution);
        dispatcher.Events.ShouldBeEmpty();
        (await OrganizationExistsAsync(markerId)).ShouldBeFalse();
    }

    [Fact]
    public async Task PostgreSqlDeadlockAfterFlushedWrite_ShouldRollbackAllTransactionEffects()
    {
        var firstLockId = Guid.NewGuid();
        var secondLockId = Guid.NewGuid();
        var markerId = Guid.NewGuid();
        await SeedLockRowsAsync(firstLockId, secondLockId);

        await using var blockerConnection = new NpgsqlConnection(factory.DatabaseConnectionString);
        await blockerConnection.OpenAsync();
        await using NpgsqlTransaction blockerTransaction = await blockerConnection.BeginTransactionAsync();
        await SetLongerDeadlockTimeoutAsync(blockerConnection, blockerTransaction);
        await LockOrganizationRowAsync(blockerConnection, blockerTransaction, secondLockId, CancellationToken.None);
        int blockerProcessId = await GetBackendProcessIdAsync(blockerConnection, blockerTransaction);

        var firstLockHeldByPosting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueToSecondLock = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatcher = new RecordingDomainEventsDispatcher();
        await using ApplicationDbContext context = CreateContext(dispatcher);
        var transaction = new EfApplicationTransaction(context);

        Task<Result> execution = transaction.ExecuteAsync(
            async cancellationToken =>
            {
                context.Organizations.Add(CreateOrganization(markerId, "DEADLOCK"));
                await context.SaveChangesAsync(cancellationToken);

                await LockOrganizationRowAsync(
                    context,
                    firstLockId,
                    cancellationToken);
                firstLockHeldByPosting.SetResult();
                await continueToSecondLock.Task.WaitAsync(cancellationToken);

                await LockOrganizationRowAsync(
                    context,
                    secondLockId,
                    cancellationToken);
                return Result.Success();
            },
            CancellationToken.None);

        Task? blockerWait = null;

        try
        {
            await firstLockHeldByPosting.Task.WaitAsync(TimeSpan.FromSeconds(5));
#pragma warning disable CA2025 // The task is awaited after rollback in the finally block below.
            blockerWait = LockOrganizationRowAsync(
                blockerConnection,
                blockerTransaction,
                firstLockId,
                CancellationToken.None);
#pragma warning restore CA2025
            await WaitUntilSessionWaitsAsync(
                factory.DatabaseConnectionString,
                blockerProcessId,
                TimeSpan.FromSeconds(5));
            continueToSecondLock.SetResult();

            Exception exception = await CaptureExceptionAsync(execution);
            FindPostgresException(exception).SqlState.ShouldBe(PostgresErrorCodes.DeadlockDetected);
            await blockerWait.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            if (!execution.IsCompleted)
            {
                continueToSecondLock.TrySetCanceled();
                await CaptureExceptionAsync(execution);
            }

            await blockerTransaction.RollbackAsync();
            if (blockerWait is not null)
            {
                await blockerWait.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        dispatcher.Events.ShouldBeEmpty();
        (await OrganizationExistsAsync(markerId)).ShouldBeFalse();
    }

    private ApplicationDbContext CreateContext(IDomainEventsDispatcher dispatcher)
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(factory.DatabaseConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new ApplicationDbContext(options, dispatcher);
    }

    private async Task SeedLockRowsAsync(Guid firstLockId, Guid secondLockId)
    {
        await using ApplicationDbContext context = CreateContext(new RecordingDomainEventsDispatcher());
        context.Organizations.AddRange(
            CreateOrganization(firstLockId, "LOCKA"),
            CreateOrganization(secondLockId, "LOCKB"));
        await context.SaveChangesAsync();
    }

    private async Task<bool> OrganizationExistsAsync(Guid organizationId)
    {
        await using ApplicationDbContext verification = CreateContext(new RecordingDomainEventsDispatcher());
        return await verification.Organizations.AnyAsync(item => item.Id == organizationId);
    }

    private static Organization CreateOrganization(Guid id, string prefix) => Organization.Create(
        id,
        $"{prefix} organization {id:N}",
        $"{prefix}-{id:N}"[..16]);

    private static async Task LockOrganizationRowAsync(
        ApplicationDbContext context,
        Guid organizationId,
        CancellationToken cancellationToken) =>
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM public.organizations WHERE id = {organizationId} FOR UPDATE",
            cancellationToken);

    private static async Task LockOrganizationRowAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT 1 FROM public.organizations WHERE id = @organization_id FOR UPDATE",
            connection,
            transaction);
        command.Parameters.AddWithValue("organization_id", organizationId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SetLongerDeadlockTimeoutAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction)
    {
        await using var command = new NpgsqlCommand(
            "SET LOCAL deadlock_timeout = '5000ms'",
            connection,
            transaction);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> GetBackendProcessIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction)
    {
        await using var command = new NpgsqlCommand("SELECT pg_backend_pid()", connection, transaction);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task WaitUntilSessionWaitsAsync(
        string connectionString,
        int processId,
        TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        await using var observer = new NpgsqlConnection(connectionString);
        await observer.OpenAsync(cancellation.Token);

        while (true)
        {
            await using var command = new NpgsqlCommand(
                "SELECT EXISTS (SELECT 1 FROM pg_locks WHERE pid = @pid AND NOT granted)",
                observer);
            command.Parameters.AddWithValue("pid", processId);
            if ((bool)(await command.ExecuteScalarAsync(cancellation.Token))!)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellation.Token);
        }
    }

    private static async Task<Exception> CaptureExceptionAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception)
        {
            return exception;
        }

        throw new ShouldAssertException("The PostgreSQL deadlock transaction unexpectedly committed.");
    }

    private static PostgresException FindPostgresException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException)
            {
                return postgresException;
            }
        }

        throw new ShouldAssertException($"Expected a PostgreSQL exception, got {exception.GetType().FullName}.");
    }

    private sealed class RecordingDomainEventsDispatcher : IDomainEventsDispatcher
    {
        internal List<IDomainEvent> Events { get; } = [];

        public Task DispatchAsync(
            IEnumerable<IDomainEvent> domainEvents,
            CancellationToken cancellationToken = default)
        {
            Events.AddRange(domainEvents);
            return Task.CompletedTask;
        }
    }
}
