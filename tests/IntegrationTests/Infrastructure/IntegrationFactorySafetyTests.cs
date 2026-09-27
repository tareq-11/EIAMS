using System.Diagnostics;
using System.Text.Json;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IntegrationTests.Infrastructure;

[Collection(nameof(IntegrationTestCollection))]
public sealed class IntegrationFactorySafetyTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task DefaultIntegrationFactory_ShouldUseDisposablePostgreSql()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        context.Database.ProviderName.ShouldBe("Npgsql.EntityFrameworkCore.PostgreSQL");
        context.Database.GetDbConnection().Database.ShouldBe("clean_architecture_integration_test");
        factory.DatabaseConnectionString.ShouldContain("Host=", Case.Insensitive);
    }

    [Fact]
    public void IntegrationTestProject_ShouldNotReferenceInMemoryOrSqliteProviders()
    {
        string projectPath = Path.Combine(FindRepositoryRoot(), "tests", "IntegrationTests", "IntegrationTests.csproj");
        string project = File.ReadAllText(projectPath);

        project.ShouldContain("Testcontainers.PostgreSql");
        project.ShouldNotContain("EntityFrameworkCore.InMemory");
        project.ShouldNotContain("EntityFrameworkCore.Sqlite");
        project.ShouldNotContain("Microsoft.Data.Sqlite");
    }

    [Fact]
    public async Task MigrationAndRbacPreflights_ShouldRunAgainstOnlyTheDisposableIntegrationDatabase()
    {
        string repositoryRoot = FindRepositoryRoot();
        NpgsqlConnectionStringBuilder connection = new(factory.DatabaseConnectionString);
        string host = RequiredConnectionValue(connection.Host, "Host");
        string username = RequiredConnectionValue(connection.Username, "Username");
        string password = RequiredConnectionValue(connection.Password, "Password");
        string database = RequiredConnectionValue(connection.Database, "Database");
        string databaseUri = $"postgresql://{Uri.EscapeDataString(username)}:" +
                             $"{Uri.EscapeDataString(password)}@{host}:{connection.Port}/" +
                             Uri.EscapeDataString(database);

        ProcessResult migration = await RunScriptAsync(
            Path.Combine(repositoryRoot, "scripts", "run-migration-preflight.sh"),
            new Dictionary<string, string>
            {
                ["EIAMS_MIGRATION_TARGET_NON_NEON"] = "1",
                ["MIGRATION_DATABASE_URL"] = databaseUri
            });
        migration.ExitCode.ShouldBe(0, migration.StandardError);
        migration.StandardOutput.ShouldContain("EF migration history");
        migration.StandardOutput.ShouldNotContain(databaseUri);

        string artifact = Path.Combine(Path.GetTempPath(), $"eiams-rbac-preflight-{Guid.NewGuid():N}.json");
        try
        {
            ProcessResult rbac = await RunScriptAsync(
                Path.Combine(repositoryRoot, "scripts", "run-rbac-v1-preflight.sh"),
                new Dictionary<string, string>
                {
                    ["RBAC_V2_ALLOW_NON_PRODUCTION"] = "1",
                    ["RBAC_V2_PGHOST"] = host,
                    ["RBAC_V2_PGPORT"] = connection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["RBAC_V2_PGDATABASE"] = database,
                    ["RBAC_V2_PGUSER"] = username,
                    ["RBAC_V2_PGPASSWORD"] = password,
                    ["RBAC_V2_OUTPUT"] = artifact
                });
            using var evidence = JsonDocument.Parse(await File.ReadAllTextAsync(artifact));
            JsonElement databaseEvidence = evidence.RootElement.GetProperty("database");
            string? databaseStatus = databaseEvidence.GetProperty("status").GetString();
            databaseStatus.ShouldBeOneOf("PASS", "UNAVAILABLE");
            string? overallStatus = evidence.RootElement.GetProperty("overallStatus").GetString();
            overallStatus.ShouldBeOneOf("PASS", "WARN");

            rbac.ExitCode.ShouldBe(overallStatus == "PASS" ? 0 : 4, rbac.StandardError);
            rbac.StandardOutput.ShouldContain($"status={overallStatus}");
            rbac.StandardOutput.ShouldNotContain(databaseUri);
        }
        finally
        {
            if (File.Exists(artifact))
            {
                File.Delete(artifact);
            }
        }
    }

    private static async Task<ProcessResult> RunScriptAsync(string scriptPath, IReadOnlyDictionary<string, string> variables)
    {
        var startInfo = new ProcessStartInfo("bash")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(scriptPath);
        foreach ((string key, string value) in variables)
        {
            startInfo.Environment[key] = value;
        }

        using Process process = Process.Start(startInfo)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        return new ProcessResult(process.ExitCode, await output, await error);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private static string RequiredConnectionValue(string? value, string name) =>
        value ?? throw new InvalidOperationException($"Disposable PostgreSQL connection is missing {name}.");

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CleanArchitectureTemplate.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root for integration-factory verification.");
    }
}
