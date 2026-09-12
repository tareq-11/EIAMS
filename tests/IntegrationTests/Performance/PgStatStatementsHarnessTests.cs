using System.Text.Json;
using Xunit.Abstractions;

namespace IntegrationTests.Performance;

public sealed class PgStatStatementsHarnessConfigurationTests
{
    [Fact]
    public void Configuration_RequiresExplicitGateAndAdditionalMediumOptIn()
    {
        Should.Throw<InvalidOperationException>(() => PgStatStatementsHarnessConfiguration.FromEnvironment(_ => null));
        var quick = PgStatStatementsHarnessConfiguration.FromEnvironment(name =>
            name == PgStatStatementsHarness.GateEnvironmentVariable ? "1" : null);
        quick.Profile.ShouldBe(PgStatStatementsHarnessProfile.Quick);
        quick.WorkloadExecutions.ShouldBe(6);

        Should.Throw<InvalidOperationException>(() => PgStatStatementsHarnessConfiguration.FromEnvironment(name => name switch
        {
            PgStatStatementsHarness.GateEnvironmentVariable => "1",
            "EIAMS_PG_STAT_STATEMENTS_PROFILE" => "Medium",
            _ => null
        }));
    }

    [Theory]
    [InlineData("Host=ep-example.neon.tech;Database=integration_test;Username=test", "Test")]
    [InlineData("Host=database.example.com;Database=integration_test;Username=test", "Test")]
    [InlineData("Host=127.0.0.1,database.example.com;Database=integration_test;Username=test", "Test")]
    [InlineData("Host=localhost;Database=application;Username=test", "Test")]
    [InlineData("Host=localhost;Database=integration_test;Username=test", "Production")]
    public void Guard_RejectsNeonNonTestAndNonTestDatabase(string connectionString, string environment)
    {
        Should.Throw<InvalidOperationException>(() =>
            PgStatStatementsHarness.ValidateTestOnlyConnection(connectionString, environment));
    }

    [Fact]
    public void TestcontainersServerCommand_IsOffByDefaultAndRequiresExplicitGate()
    {
        IntegrationTestWebAppFactory.GetPostgreSqlServerCommand(_ => null).ShouldBeEmpty();
        IntegrationTestWebAppFactory.GetPostgreSqlServerCommand(name =>
            name == PgStatStatementsHarness.GateEnvironmentVariable ? "1" : null)
            .ShouldBe(["-c", "shared_preload_libraries=pg_stat_statements"]);
    }

    [Fact]
    public void Parsing_UsesOnlySafeHashedFingerprintsAndCurrentMetrics()
    {
        IReadOnlyList<PgStatStatementsStatement> result = PgStatStatementsHarness.ParseRows(
        [
            new("SELECT * FROM users WHERE email = 'admin@example.test'", 3, 12.5, 4.16, 3),
            new("SELECT 1", 0, 1, 1, 1)
        ]);

        result.Count.ShouldBe(1);
        result[0].Fingerprint.ShouldStartWith("sql-sha256-");
        result[0].Fingerprint.ShouldNotContain("admin@example.test");
        result[0].Calls.ShouldBe(3);
        result[0].TotalExecutionTimeMs.ShouldBe(12.5);
        result[0].MeanExecutionTimeMs.ShouldBe(4.16);
    }

    [Fact]
    public async Task ArtifactWriter_IsAtomicAndDoesNotPersistRawSql()
    {
        string directory = Path.Combine(Path.GetTempPath(), "eiams-pg-stat-statements-results", $"test-{Guid.NewGuid():N}");
        try
        {
            var evidence = new PgStatStatementsEvidence("passed", "Quick",
                new("17.0", "1.11", ["query", "calls", "total_exec_time", "mean_exec_time", "rows"]),
                new(6, 10, 5, "bounded"),
                [new(PgStatStatementsHarness.Fingerprint("SELECT $1"), 6, 1, .16, 6)],
                "redacted", "not endpoint attribution");
            string path = await PgStatStatementsArtifactWriter.WriteAsync(directory, evidence);
            string json = await File.ReadAllTextAsync(path);

            json.ShouldNotContain("SELECT $1", Case.Insensitive);
            json.ShouldNotContain("admin@example.test", Case.Insensitive);
            Directory.GetFiles(directory, "*.tmp").ShouldBeEmpty();
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}

[Collection(nameof(IntegrationTestCollection))]
public sealed class PgStatStatementsHarnessIntegrationTests(
    IntegrationTestWebAppFactory factory,
    ITestOutputHelper output)
{
    [ExplicitPgStatStatementsHarnessFact]
    [Trait("Category", "Performance")]
    [Trait("WorkloadClass", PerformanceWorkloadContracts.NormalExpectedTraffic)]
    public async Task QuickHarness_CollectsCurrentMetricsAndWritesOnlyRedactedEvidence()
    {
        var configuration = PgStatStatementsHarnessConfiguration.FromEnvironment();
        PgStatStatementsEvidence evidence = await PgStatStatementsHarness.CaptureQuickAsync(
            factory.DatabaseConnectionString, "Test", configuration);
        string path = await PgStatStatementsArtifactWriter.WriteAsync(configuration.ResultDirectory, evidence);
        string json = await File.ReadAllTextAsync(path);

        evidence.Status.ShouldBe("passed");
        evidence.Server.SupportedColumns.ShouldContain("total_exec_time");
        evidence.Server.SupportedColumns.ShouldContain("mean_exec_time");
        evidence.TopStatements.ShouldNotBeEmpty();
        evidence.TopStatements.ShouldAllBe(statement => statement.Fingerprint.StartsWith("sql-sha256-", StringComparison.Ordinal));
        json.ShouldNotContain("SELECT", Case.Insensitive);
        json.ShouldNotContain("integration-admin@example.com", Case.Insensitive);
        output.WriteLine($"pg_stat_statements evidence: {path}");
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ExplicitPgStatStatementsHarnessFactAttribute : FactAttribute
{
    public ExplicitPgStatStatementsHarnessFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(PgStatStatementsHarness.GateEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            Skip = "Explicit pg_stat_statements harness. Set RUN_PG_STAT_STATEMENTS_HARNESS=1; Medium additionally requires EIAMS_ALLOW_LONG_PG_STAT_STATEMENTS_HARNESS=1.";
        }
    }
}
