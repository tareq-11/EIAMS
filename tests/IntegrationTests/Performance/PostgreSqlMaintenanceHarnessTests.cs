using System.Text.Json;
using Npgsql;
using Xunit.Abstractions;

namespace IntegrationTests.Performance;

public sealed class PostgreSqlMaintenanceHarnessConfigurationTests
{
    [Fact]
    public void Configuration_IsClosedByDefault_AndRefusesUnimplementedMedium()
    {
        Should.Throw<InvalidOperationException>(() => PostgreSqlMaintenanceHarnessConfiguration.FromEnvironment(_ => null));

        var quick = PostgreSqlMaintenanceHarnessConfiguration.FromEnvironment(name =>
            name == PostgreSqlMaintenanceHarness.GateEnvironmentVariable ? "1" : null);
        quick.Profile.ShouldBe(PostgreSqlMaintenanceHarnessProfile.Quick);

        Should.Throw<InvalidOperationException>(() => PostgreSqlMaintenanceHarnessConfiguration.FromEnvironment(name => name switch
        {
            PostgreSqlMaintenanceHarness.GateEnvironmentVariable => "1",
            "EIAMS_POSTGRES_MAINTENANCE_PROFILE" => "Medium",
            "EIAMS_ALLOW_MEDIUM_POSTGRES_MAINTENANCE_HARNESS" => "1",
            _ => null
        }));
    }

    [Theory]
    [InlineData("Host=ep-example.neon.tech;Database=integration_test;Username=test", "Test")]
    [InlineData("Host=database.example.com;Database=integration_test;Username=test", "Test")]
    [InlineData("Host=localhost;Database=another_test;Username=test", "Test")]
    [InlineData("Host=localhost;Database=application;Username=test", "Test")]
    [InlineData("Host=localhost;Database=integration_test;Username=test", "Production")]
    public void Guard_RejectsNeonRemoteAndNonTestTargets(string connectionString, string environmentName)
    {
        Should.Throw<InvalidOperationException>(() =>
            PostgreSqlMaintenanceHarness.ValidateTestOnlyConnection(connectionString, environmentName));
    }

    [Fact]
    public void ForeignKeySummary_AccountsForOnlyDocumentedExclusions()
    {
        ForeignKeyIndexSummary summary = PostgreSqlMaintenanceHarness.SummarizeForeignKeys(8, 7, 0);

        summary.MissingSupportingIndexCount.ShouldBe(1);
        summary.ExplicitlyExcludedForeignKeyCount.ShouldBe(0);
        Should.Throw<ArgumentOutOfRangeException>(() => PostgreSqlMaintenanceHarness.SummarizeForeignKeys(1, 2, 0));
    }

    [Fact]
    public async Task ArtifactWriter_IsAtomicAndContainsNoCatalogOrQueryNames()
    {
        string directory = Path.Combine(Path.GetTempPath(), "eiams-postgres-maintenance-results", $"test-{Guid.NewGuid():N}");
        try
        {
            var evidence = new PostgreSqlMaintenanceEvidence("passed", "Quick", new(5, 5_000, 2, "read-only"),
                new(4, 4, 0, 0), new(5, 5, 10, 1, 2), new(9, 2, 0, 1),
                [new("credential-lookup-component", new(1, 2, 3, 4, 5, 1, 2, 0, 0, 0, ["Index Scan"]))],
                "redacted", "component-only");
            string path = await PostgreSqlMaintenanceArtifactWriter.WriteAsync(directory, evidence);
            string json = await File.ReadAllTextAsync(path);

            json.ShouldNotContain("public.users", Case.Insensitive);
            json.ShouldNotContain("SELECT", Case.Insensitive);
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
public sealed class PostgreSqlMaintenanceHarnessIntegrationTests(
    IntegrationTestWebAppFactory factory,
    ITestOutputHelper output)
{
    [Fact]
    public async Task ForeignKeyIndexAudit_RequiresFullNonPartialLeadingColumns()
    {
        PostgreSqlMaintenanceHarness.ValidateTestOnlyConnection(factory.DatabaseConnectionString, "Test");
        await using var connection = new NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        string suffix = Guid.NewGuid().ToString("N");
        string parent = $"fk_index_parent_{suffix}";
        string shortChild = $"fk_index_short_{suffix}";
        string partialChild = $"fk_index_partial_{suffix}";
        string fullChild = $"fk_index_full_{suffix}";

        try
        {
            ForeignKeyIndexSummary baseline = await PostgreSqlMaintenanceHarness.ReadForeignKeyIndexSummaryAsync(
                connection, transaction, CancellationToken.None);
            await CreateForeignKeyFixtureAsync(connection, transaction, parent, shortChild, partialChild, fullChild);
            ForeignKeyIndexSummary fixture = await PostgreSqlMaintenanceHarness.ReadForeignKeyIndexSummaryAsync(
                connection, transaction, CancellationToken.None);

            fixture.TotalForeignKeyCount.ShouldBe(baseline.TotalForeignKeyCount + 3);
            fixture.SupportingIndexCount.ShouldBe(baseline.SupportingIndexCount + 1);
            fixture.MissingSupportingIndexCount.ShouldBe(baseline.MissingSupportingIndexCount + 2);
        }
        finally
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }

        await using var cleanupCommand = new NpgsqlCommand("SELECT to_regclass(@relation_name)::text", connection);
        cleanupCommand.Parameters.AddWithValue("relation_name", $"public.{parent}");
        object? cleanupResult = await cleanupCommand.ExecuteScalarAsync();
        (cleanupResult is null || cleanupResult is DBNull).ShouldBeTrue();
    }

    [ExplicitPostgreSqlMaintenanceHarnessFact]
    [Trait("Category", "Performance")]
    [Trait("WorkloadClass", PerformanceWorkloadContracts.NormalExpectedTraffic)]
    public async Task QuickHarness_ReviewsCatalogAndWritesOnlyRedactedEvidence()
    {
        var configuration = PostgreSqlMaintenanceHarnessConfiguration.FromEnvironment();
        PostgreSqlMaintenanceEvidence evidence = await PostgreSqlMaintenanceHarness.CaptureAsync(
            factory.DatabaseConnectionString, "Test", configuration);
        string path = await PostgreSqlMaintenanceArtifactWriter.WriteAsync(configuration.ResultDirectory, evidence);
        string json = await File.ReadAllTextAsync(path);

        evidence.ForeignKeys.TotalForeignKeyCount.ShouldBeGreaterThan(0);
        (evidence.Status == "passed" || evidence.Status == "review_required").ShouldBeTrue();
        evidence.Indexes.InvalidIndexCount.ShouldBe(0);
        evidence.RepresentativePlans.Count.ShouldBe(2);
        evidence.RepresentativePlans.ShouldAllBe(item => item.Plan.NodeTypes.Count > 0);
        json.ShouldNotContain("public.users", Case.Insensitive);
        json.ShouldNotContain("warehouse_documents", Case.Insensitive);
        json.ShouldNotContain("SELECT", Case.Insensitive);
        json.ShouldNotContain(IntegrationTestWebAppFactory.AdministratorEmail, Case.Insensitive);
        output.WriteLine($"PostgreSQL maintenance evidence: {path}");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Fixture identifiers are generated solely from Guid hexadecimal text in this test and are used only in the disposable Testcontainers database transaction.")]
    private static async Task CreateForeignKeyFixtureAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string parent,
        string shortChild,
        string partialChild,
        string fullChild)
    {
        string[] commands =
        [
            $"CREATE TABLE public.\"{parent}\" (first_key integer NOT NULL, second_key integer NOT NULL, PRIMARY KEY (first_key, second_key))",
            $"CREATE TABLE public.\"{shortChild}\" (id integer PRIMARY KEY, first_key integer NOT NULL, second_key integer NOT NULL, FOREIGN KEY (first_key, second_key) REFERENCES public.\"{parent}\" (first_key, second_key))",
            $"CREATE INDEX \"ix_{shortChild}_first\" ON public.\"{shortChild}\" (first_key)",
            $"CREATE TABLE public.\"{partialChild}\" (id integer PRIMARY KEY, first_key integer NOT NULL, second_key integer NOT NULL, FOREIGN KEY (first_key, second_key) REFERENCES public.\"{parent}\" (first_key, second_key))",
            $"CREATE INDEX \"ix_{partialChild}_both\" ON public.\"{partialChild}\" (first_key, second_key) WHERE first_key > 0",
            $"CREATE TABLE public.\"{fullChild}\" (id integer PRIMARY KEY, first_key integer NOT NULL, second_key integer NOT NULL, FOREIGN KEY (first_key, second_key) REFERENCES public.\"{parent}\" (first_key, second_key))",
            $"CREATE INDEX \"ix_{fullChild}_both\" ON public.\"{fullChild}\" (first_key, second_key)"
        ];
        foreach (string sql in commands)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            await command.ExecuteNonQueryAsync();
        }
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ExplicitPostgreSqlMaintenanceHarnessFactAttribute : FactAttribute
{
    public ExplicitPostgreSqlMaintenanceHarnessFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(PostgreSqlMaintenanceHarness.GateEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            Skip = "Explicit PostgreSQL maintenance harness. Set RUN_POSTGRES_MAINTENANCE_HARNESS=1; only bounded Quick is implemented.";
        }
    }
}
