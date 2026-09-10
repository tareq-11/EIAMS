using Npgsql;
using System.Data.Common;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using IntegrationTests.Performance;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// Small Testcontainers-only checks for the client pool's bounded failure and recovery behavior.
/// They deliberately use one held connection and never target Neon or generate load.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public sealed class DatabaseConnectionSafetyTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task PoolExhaustion_ShouldFailWithinConfiguredTimeout_AndRecoverAfterLeaseIsReturned()
    {
        string connectionString = CreateSingleConnectionPoolConnectionString();
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using NpgsqlConnection holder = await dataSource.OpenConnectionAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await Should.ThrowAsync<NpgsqlException>(async () =>
        {
            await using NpgsqlConnection contender = await dataSource.OpenConnectionAsync(timeout.Token);
        });

        await holder.CloseAsync();
        await using NpgsqlConnection recovered = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT 1", recovered);
        (await command.ExecuteScalarAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task CancelledCommand_ShouldReturnPoolCapacityForTheNextRequest()
    {
        string connectionString = CreateSingleConnectionPoolConnectionString();
        await using var dataSource = NpgsqlDataSource.Create(connectionString);

        await using (NpgsqlConnection cancelledConnection = await dataSource.OpenConnectionAsync())
        await using (var delayedCommand = new NpgsqlCommand("SELECT pg_sleep(5)", cancelledConnection))
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            await Should.ThrowAsync<OperationCanceledException>(() => delayedCommand.ExecuteNonQueryAsync(cancellation.Token));
        }

        await using NpgsqlConnection recovered = await dataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT 1", recovered);
        (await command.ExecuteScalarAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task EfConnectionCheckout_ShouldApplyStatementAndLockTimeoutsOnEachScope()
    {
        string[] first = await ReadSessionTimeoutsAsync();
        string[] second = await ReadSessionTimeoutsAsync();

        first.ShouldBe(["25s", "20s"]);
        second.ShouldBe(["25s", "20s"]);
    }

    [Fact]
    public async Task EfConnectionCheckout_ShouldNotIssueAdditionalSetCommands()
    {
        SqlCommandCounterInterceptor counter = factory.Services.GetRequiredService<SqlCommandCounterInterceptor>();
        counter.Reset();

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.OpenConnectionAsync();
        try
        {
            await context.Users.AsNoTracking().AnyAsync();
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }

        counter.GetCommandTexts().Any(command =>
            command.TrimStart().StartsWith("SET ", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
    }

    private string CreateSingleConnectionPoolConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder(factory.DatabaseConnectionString)
        {
            Pooling = true,
            MaxPoolSize = 1,
            MinPoolSize = 0,
            Timeout = 1,
            CommandTimeout = 10,
            CancellationTimeout = 2,
            KeepAlive = 0
        };

        return builder.ConnectionString;
    }

    private async Task<string[]> ReadSessionTimeoutsAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.OpenConnectionAsync();

        try
        {
            await using DbCommand command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SHOW statement_timeout; SHOW lock_timeout;";
            await using DbDataReader reader = await command.ExecuteReaderAsync();
            var values = new List<string>();
            do
            {
                if (await reader.ReadAsync())
                {
                    values.Add(reader.GetString(0));
                }
            }
            while (await reader.NextResultAsync());

            return values.ToArray();
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
