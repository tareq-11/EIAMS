using System.Diagnostics.CodeAnalysis;
using System.Data.Common;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Infrastructure.Materials;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IntegrationTests.Infrastructure;

[Collection(nameof(IntegrationTestCollection))]
public sealed class PostgresMaterialOperationLockTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task AcquireAsync_WithMultipleIdsInsideTransaction_ShouldAcquireLocks()
    {
        string database = $"lock_multi_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync();

            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            var id3 = Guid.NewGuid();

            PostgresMaterialOperationLock materialLock = new(context);
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
            await materialLock.AcquireAsync(new[] { id1, id2, id3 }, CancellationToken.None);
            await AssertAdvisoryLockCountAsync(context, atLeast: 3);
            await transaction.CommitAsync();
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task AcquireAsync_WithSingleId_ShouldAcquireLock()
    {
        string database = $"lock_single_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync();

            var id = Guid.NewGuid();

            PostgresMaterialOperationLock materialLock = new(context);
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
            await materialLock.AcquireAsync(new[] { id }, CancellationToken.None);
            await AssertAdvisoryLockCountAsync(context, atLeast: 1);
            await transaction.CommitAsync();
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task AcquireAsync_WithDuplicateIds_ShouldDeduplicateAndAcquire()
    {
        string database = $"lock_dup_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync();

            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();

            PostgresMaterialOperationLock materialLock = new(context);
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
            await materialLock.AcquireAsync(new[] { id1, id2, id1, id2 }, CancellationToken.None);
            await AssertAdvisoryLockCountAsync(context, atLeast: 2);
            await transaction.CommitAsync();
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task AcquireAsync_WithoutTransaction_ShouldThrowInvalidOperationException()
    {
        string database = $"lock_no_tx_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync();

            var id = Guid.NewGuid();

            PostgresMaterialOperationLock materialLock = new(context);

            await Should.ThrowAsync<InvalidOperationException>(
                () => materialLock.AcquireAsync(new[] { id }, CancellationToken.None));
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task AcquireAsync_WithEmptyIds_ShouldReturnWithoutAcquiringAnyLock()
    {
        string database = $"lock_empty_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync();

            PostgresMaterialOperationLock materialLock = new(context);
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
            await materialLock.AcquireAsync(Array.Empty<Guid>(), CancellationToken.None);
            await AssertAdvisoryLockCountAsync(context, atLeast: 0);
            await transaction.CommitAsync();
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task AcquireAsync_InsideSubmitPath_ShouldNotFailDueToLock()
    {
        string database = $"lock_submit_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync();

            PostgresMaterialOperationLock materialLock = new(context);
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
            Guid[] materialIds = { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            await materialLock.AcquireAsync(materialIds, CancellationToken.None);
            await AssertAdvisoryLockCountAsync(context, atLeast: 3);
            await transaction.CommitAsync();
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task AcquireAsync_InsideUpdatePath_ShouldNotFailDueToLock()
    {
        string database = $"lock_update_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext context = CreateContext(connectionString);
            await context.Database.MigrateAsync();

            PostgresMaterialOperationLock materialLock = new(context);
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
            Guid[] materialIds = { Guid.NewGuid(), Guid.NewGuid() };
            await materialLock.AcquireAsync(materialIds, CancellationToken.None);
            await AssertAdvisoryLockCountAsync(context, atLeast: 2);
            await transaction.CommitAsync();
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task AcquireAsync_ShouldSerializeOverlappingTransactionsAndReleaseLockAtCommit()
    {
        string database = $"lock_contention_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext firstContext = CreateContext(connectionString);
            await firstContext.Database.MigrateAsync();
            await using ApplicationDbContext secondContext = CreateContext(connectionString);
            PostgresMaterialOperationLock firstLock = new(firstContext);
            PostgresMaterialOperationLock secondLock = new(secondContext);
            var materialId = Guid.NewGuid();

            await using IDbContextTransaction firstTransaction = await firstContext.Database.BeginTransactionAsync();
            await firstLock.AcquireAsync([materialId], CancellationToken.None);
            await AssertAdvisoryLockCountAsync(firstContext, atLeast: 1);

            Task secondTransactionTask = AcquireAndCommitAsync();
            Task firstCompletion = await Task.WhenAny(secondTransactionTask, Task.Delay(TimeSpan.FromMilliseconds(250)));
            firstCompletion.ShouldNotBe(secondTransactionTask, "the second transaction must wait while the first owns the xact lock");

            await firstTransaction.CommitAsync();
            await secondTransactionTask.WaitAsync(TimeSpan.FromSeconds(5));

            async Task AcquireAndCommitAsync()
            {
                await using IDbContextTransaction secondTransaction = await secondContext.Database.BeginTransactionAsync();
                await secondLock.AcquireAsync([materialId], CancellationToken.None);
                await secondTransaction.CommitAsync();
            }
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [Fact]
    [SuppressMessage(
        "Reliability",
        "CA2025:Ensure tasks using 'IDisposable' instances complete before the instances are disposed",
        Justification = "Both lock-acquisition tasks are awaited with Task.WhenAll before either context leaves scope.")]
    public async Task AcquireAsync_WithOppositeInputOrders_ShouldNotDeadlock()
    {
        string database = $"lock_opposite_order_{Guid.NewGuid():N}";
        string connectionString = await CreateDatabaseAsync(database);
        try
        {
            await using ApplicationDbContext firstContext = CreateContext(connectionString);
            await firstContext.Database.MigrateAsync();
            await using ApplicationDbContext secondContext = CreateContext(connectionString);
            PostgresMaterialOperationLock firstLock = new(firstContext);
            PostgresMaterialOperationLock secondLock = new(secondContext);
            var firstId = Guid.NewGuid();
            var secondId = Guid.NewGuid();
            var bothTransactionsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int startedCount = 0;

            async Task AcquireAsync(
                ApplicationDbContext context,
                PostgresMaterialOperationLock materialLock,
                Guid[] ids)
            {
                await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
                if (Interlocked.Increment(ref startedCount) == 2)
                {
                    bothTransactionsStarted.SetResult();
                }

                await bothTransactionsStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await materialLock.AcquireAsync(ids, CancellationToken.None);
                await transaction.CommitAsync();
            }

            Task first = AcquireAsync(firstContext, firstLock, [firstId, secondId]);
            Task second = AcquireAsync(secondContext, secondLock, [secondId, firstId]);

            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
            first.IsCompletedSuccessfully.ShouldBeTrue();
            second.IsCompletedSuccessfully.ShouldBeTrue();
        }
        finally
        {
            await DropDatabaseAsync(database);
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "This fixed PostgreSQL catalog query uses no user-supplied SQL or values.")]
    private static async Task AssertAdvisoryLockCountAsync(ApplicationDbContext context, int atLeast)
    {
        await using DbCommand command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = context.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT COUNT(*) FROM pg_locks " +
            "WHERE locktype = 'advisory' AND pid = pg_backend_pid() AND granted";
        long lockCount = (long)(await command.ExecuteScalarAsync())!;
        lockCount.ShouldBeGreaterThanOrEqualTo(atLeast);
    }

    private ApplicationDbContext CreateContext(string connectionString)
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(
                typeof(ApplicationDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new ApplicationDbContext(options, factory.Services.GetRequiredService<IDomainEventsDispatcher>());
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Database name is a deterministic Guid generated inside a disposable Testcontainers database.")]
    private async Task<string> CreateDatabaseAsync(string database)
    {
        var builder = new NpgsqlConnectionStringBuilder(factory.DatabaseConnectionString) { Database = "postgres" };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
        await cmd.ExecuteNonQueryAsync();
        builder.Database = database;
        return builder.ConnectionString;
    }

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Database name is a deterministic Guid generated inside a disposable Testcontainers database.")]
    private async Task DropDatabaseAsync(string database)
    {
        var builder = new NpgsqlConnectionStringBuilder(factory.DatabaseConnectionString) { Database = "postgres" };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", connection);
        await cmd.ExecuteNonQueryAsync();
    }
}
