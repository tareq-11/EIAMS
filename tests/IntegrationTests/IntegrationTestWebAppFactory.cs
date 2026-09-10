using Application.Abstractions.Authentication;
using Domain.Common;
using Domain.Roles;
using Domain.UserRoleScopes;
using Domain.Users;
using Infrastructure.Database;
using IntegrationTests.Performance;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Web.Api;

namespace IntegrationTests;

public sealed class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    internal const string AdministratorEmail = "integration-admin@example.com";
    internal const string AdministratorPassword = "Password123!";
    internal const string JwtSecret = "super-duper-secret-value-that-should-be-in-user-secrets";
    internal const string JwtIssuer = "clean-architecture-template";
    internal const string JwtAudience = "developers";

    private readonly string attachmentStoragePath = Path.Combine(
        Path.GetTempPath(),
        $"eiams-integration-attachments-{Guid.NewGuid():N}");

    private readonly PostgreSqlContainer _dbContainer = CreatePostgreSqlContainer();

    internal string DatabaseConnectionString => _dbContainer.GetConnectionString();

    /// <summary>
    /// Keeps the normal integration-suite database baseline unchanged. The preload library is a
    /// server-start setting, so it is attached only for the explicitly gated measurement run.
    /// </summary>
    internal static string[] GetPostgreSqlServerCommand(Func<string, string?>? getEnvironmentVariable = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;
        return string.Equals(
            getEnvironmentVariable(PgStatStatementsHarness.GateEnvironmentVariable),
            "1",
            StringComparison.Ordinal)
            ? ["-c", "shared_preload_libraries=pg_stat_statements"]
            : [];
    }

    private static PostgreSqlContainer CreatePostgreSqlContainer()
    {
        PostgreSqlBuilder builder = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("clean_architecture_integration_test")
            .WithUsername("postgres")
            .WithPassword("postgres");
        string[] command = GetPostgreSqlServerCommand();
        if (command.Length > 0)
        {
            builder = builder.WithCommand(command);
        }
        return builder.Build();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Database", _dbContainer.GetConnectionString());

        // Provide deterministic JWT settings so tokens can be issued and validated in tests.
        builder.UseSetting("Jwt:Secret", JwtSecret);
        builder.UseSetting("Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Jwt:Audience", JwtAudience);
        builder.UseSetting("Jwt:ExpirationInMinutes", "60");
        builder.UseSetting("AllowedHosts", "localhost;127.0.0.1");
        builder.UseSetting("AttachmentStorage:Local:RootPath", attachmentStoragePath);

        // Relax rate limiting so the test suite is not throttled.
        builder.UseSetting("RateLimiting:Global:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Authentication:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Authentication:ConcurrencyLimit", "100000");
        builder.UseSetting("RateLimiting:Authentication:GlobalConcurrencyLimit", "100000");
        builder.UseSetting("RateLimiting:Concurrency:Reporting", "100000");
        builder.UseSetting("RateLimiting:Concurrency:Upload", "100000");
        builder.UseSetting("RateLimiting:Concurrency:Posting", "100000");

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<SqlCommandCounterInterceptor>();
            services.AddSingleton<DbCommandInterceptor>(serviceProvider =>
                serviceProvider.GetRequiredService<SqlCommandCounterInterceptor>());
        });
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();

        IPasswordHasher passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var administrator = User.Create(
            Guid.NewGuid(),
            AdministratorEmail,
            "Integration",
            "Administrator",
            passwordHasher.Hash(AdministratorPassword));

        dbContext.Users.Add(administrator);
        dbContext.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(),
            administrator.Id,
            WellKnownRoles.AdministratorId,
            ScopeType.Enterprise,
            scopeId: null));

        await dbContext.SaveChangesAsync();
    }

    internal BenchmarkProfiledWebAppFactory CreateSiblingFactory(
        SqlCommandCounterInterceptor? commandCounter = null,
        ApiBenchmarkWorkerProfile profile = ApiBenchmarkWorkerProfile.IsolatedRequestCost) =>
        new(_dbContainer.GetConnectionString(), commandCounter, profile);

    internal async Task<PostgresAdvisoryLockLease> HoldApplicationLockAsync(
        string resourceKey,
        CancellationToken cancellationToken = default)
    {
#pragma warning disable CA2000 // Ownership is transferred to PostgresAdvisoryLockLease on success.
        var connection = new NpgsqlConnection(_dbContainer.GetConnectionString());
#pragma warning restore CA2000
        await connection.OpenAsync(cancellationToken);
        NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using var pidCommand = new NpgsqlCommand("SELECT pg_backend_pid()", connection, transaction);
            int holderProcessId = (int)(await pidCommand.ExecuteScalarAsync(cancellationToken))!;

            await using var lockCommand = new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(hashtextextended(@resource_key, 0))",
                connection,
                transaction);
            lockCommand.Parameters.AddWithValue("resource_key", resourceKey);
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);

            return new PostgresAdvisoryLockLease(
                _dbContainer.GetConnectionString(),
                connection,
                transaction,
                holderProcessId);
        }
        catch
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
            throw;
        }
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();

        if (Directory.Exists(attachmentStoragePath))
        {
            Directory.Delete(attachmentStoragePath, recursive: true);
        }
    }

    internal sealed class PostgresAdvisoryLockLease(
        string connectionString,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int holderProcessId) : IAsyncDisposable
    {
        private bool released;

        internal async Task WaitUntilContendedAsync(CancellationToken cancellationToken = default)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await using var observer = new NpgsqlConnection(connectionString);
            await observer.OpenAsync(timeout.Token);

            const string sql = """
                SELECT EXISTS (
                    SELECT 1
                    FROM pg_locks AS waiting
                    INNER JOIN pg_locks AS held
                        ON held.locktype = waiting.locktype
                       AND held.database IS NOT DISTINCT FROM waiting.database
                       AND held.classid IS NOT DISTINCT FROM waiting.classid
                       AND held.objid IS NOT DISTINCT FROM waiting.objid
                       AND held.objsubid IS NOT DISTINCT FROM waiting.objsubid
                    WHERE NOT waiting.granted
                      AND held.granted
                      AND held.pid = @holder_pid
                      AND waiting.pid <> @holder_pid
                )
                """;

            while (true)
            {
                await using var command = new NpgsqlCommand(sql, observer);
                command.Parameters.AddWithValue("holder_pid", holderProcessId);
                if ((bool)(await command.ExecuteScalarAsync(timeout.Token))!)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
            }
        }

        internal async Task ReleaseAsync()
        {
            if (released)
            {
                return;
            }

            released = true;
            await transaction.RollbackAsync();
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }

        public ValueTask DisposeAsync() => new(ReleaseAsync());
    }

    internal sealed class BenchmarkProfiledWebAppFactory(
        string connectionString,
        SqlCommandCounterInterceptor? commandCounter,
        ApiBenchmarkWorkerProfile profile) : WebApplicationFactory<Program>
    {
        internal ApiBenchmarkWorkerProfile Profile { get; } = profile;

        internal ApiBenchmarkWorkerProfileMetadata WorkerProfileMetadata { get; } =
            ApiBenchmarkWorkerProfiles.GetMetadata(profile);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Database", connectionString);
            builder.UseSetting("Jwt:Secret", JwtSecret);
            builder.UseSetting("Jwt:Issuer", JwtIssuer);
            builder.UseSetting("Jwt:Audience", JwtAudience);
            builder.UseSetting("Jwt:ExpirationInMinutes", "60");
            builder.UseSetting("AllowedHosts", "localhost;127.0.0.1");
            builder.UseSetting(
                "AttachmentStorage:Local:RootPath",
                Path.Combine(Path.GetTempPath(), $"eiams-sibling-attachments-{Guid.NewGuid():N}"));
            builder.UseSetting("RateLimiting:Global:PermitLimit", "100000");
            builder.UseSetting("RateLimiting:Authentication:PermitLimit", "100000");
            builder.UseSetting("RateLimiting:Authentication:ConcurrencyLimit", "100000");
            builder.UseSetting("RateLimiting:Authentication:GlobalConcurrencyLimit", "100000");
            builder.UseSetting("RateLimiting:Concurrency:Reporting", "100000");
            builder.UseSetting("RateLimiting:Concurrency:Upload", "100000");
            builder.UseSetting("RateLimiting:Concurrency:Posting", "100000");

            if (Profile == ApiBenchmarkWorkerProfile.FullBackgroundWorkload)
            {
                // These bounded test-only cadences guarantee a real first cycle during a short run.
                // Production configuration and worker implementations remain unchanged.
                builder.UseSetting("PolymorphicReferenceAudit:Enabled", "true");
                builder.UseSetting("PolymorphicReferenceAudit:InitialDelay", "00:00:00.050");
                builder.UseSetting("PolymorphicReferenceAudit:Interval", "00:00:00.250");
                builder.UseSetting("Idempotency:Cleanup:Enabled", "true");
                builder.UseSetting("Idempotency:Cleanup:InitialDelay", "00:00:00.050");
                builder.UseSetting("Idempotency:Cleanup:Interval", "00:00:00.250");
                builder.UseSetting("AttachmentStorage:Cleanup:PollInterval", "00:00:00.250");
            }

            builder.ConfigureServices(services =>
            {
                if (Profile == ApiBenchmarkWorkerProfile.IsolatedRequestCost)
                {
                    ApiBenchmarkWorkerProfiles.RemoveMeasuredWorkers(services);
                }

                if (commandCounter is not null)
                {
                    services.AddSingleton(commandCounter);
                    services.AddSingleton<DbCommandInterceptor>(commandCounter);
                }
            });
        }
    }
}
