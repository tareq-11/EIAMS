using System.Text.Json;

namespace IntegrationTests.Performance;

public sealed class PostgreSqlReadPlanAnalysisTests
{
    [Fact]
    public void Configuration_ShouldRequireExplicitGate_AndRequireSecondOptInForMedium()
    {
        Should.Throw<InvalidOperationException>(() =>
            PostgreSqlReadPlanAnalysisConfiguration.FromEnvironment(_ => null));

        var quick = PostgreSqlReadPlanAnalysisConfiguration.FromEnvironment(name => name switch
        {
            PostgreSqlReadPlanAnalysis.GateEnvironmentVariable => "1",
            _ => null
        });
        quick.Profile.ShouldBe(PostgreSqlReadPlanAnalysisProfile.Quick);
        quick.DatasetProfile.ShouldBe(DatasetProfile.Small);

        Should.Throw<InvalidOperationException>(() => PostgreSqlReadPlanAnalysisConfiguration.FromEnvironment(name => name switch
        {
            PostgreSqlReadPlanAnalysis.GateEnvironmentVariable => "1",
            "EIAMS_POSTGRES_PLAN_PROFILE" => "Medium",
            _ => null
        }));

        var medium = PostgreSqlReadPlanAnalysisConfiguration.FromEnvironment(name => name switch
        {
            PostgreSqlReadPlanAnalysis.GateEnvironmentVariable => "1",
            "EIAMS_POSTGRES_PLAN_PROFILE" => "Medium",
            PostgreSqlReadPlanAnalysis.LongRunOptInEnvironmentVariable => "1",
            _ => null
        });
        medium.DatasetProfile.ShouldBe(DatasetProfile.Medium);
    }

    [Fact]
    public void Selection_ShouldUseObservedReadScenariosOnly_AndOrderByScenarioP95()
    {
        IReadOnlyDictionary<ApiLoadTestScenario, ApiLoadTestScenarioMetrics> metrics = new Dictionary<ApiLoadTestScenario, ApiLoadTestScenarioMetrics>
        {
            [ApiLoadTestScenario.Login] = new(50, 50, 0, 0, 1, 2, 3),
            [ApiLoadTestScenario.ReadList] = new(50, 50, 0, 0, 1, 17, 18),
            [ApiLoadTestScenario.ReadDetail] = new(50, 50, 0, 0, 1, 9, 10),
            [ApiLoadTestScenario.Report] = new(50, 0, 50, 0, null, null, null),
            [ApiLoadTestScenario.Post] = new(50, 50, 0, 0, 1, 100, 100)
        };

        IReadOnlyList<ReadPlanSelection> result = PostgreSqlReadPlanAnalysis.SelectSlowReadCandidates(metrics);

        result.Select(item => item.LogicalName).ShouldBe(["organization-list-page-component", "organization-detail-component"]);
        foreach (ReadPlanSelection item in result)
        {
            (item.Scenario == ApiLoadTestScenario.ReadList || item.Scenario == ApiLoadTestScenario.ReadDetail).ShouldBeTrue();
        }
    }

    [Fact]
    public void ParseExplainJson_ShouldKeepOnlyBoundedSafePlanProperties()
    {
        const string plan = """
            [{
              "Planning Time": 0.25,
              "Execution Time": 1.5,
              "Plan": {
                "Node Type": "Index Scan",
                "Relation Name": "users_with_sensitive_email",
                "Total Cost": 42.5,
                "Plan Rows": 20,
                "Actual Rows": 10,
                "Actual Loops": 1,
                "Shared Hit Blocks": 3,
                "Plans": [{ "Node Type": "malicious value: admin@example.test" }]
              }
            }]
            """;

        ReadPlanSummary result = PostgreSqlReadPlanAnalysis.ParseExplainJson(plan);
        string serialized = JsonSerializer.Serialize(new ReadPlanEvidence(
            "organization-list-page-component",
            17,
            "representative-read-component-plan",
            "Scenario p95 is a selection signal only.",
            result));

        result.PlanningTimeMs.ShouldBe(0.25);
        result.ExecutionTimeMs.ShouldBe(1.5);
        result.NodeTypes.ShouldBe(["Index Scan", "redacted"]);
        serialized.ShouldNotContain("Relation Name");
        serialized.ShouldNotContain("users_with_sensitive_email");
        serialized.ShouldNotContain("admin@example.test");
        serialized.ShouldNotContain("FROM organizations", Case.Insensitive);
        serialized.ShouldNotContain("@organization_id", Case.Insensitive);
        serialized.ShouldContain("representative-read-component-plan");
        serialized.ShouldContain("selection signal", Case.Insensitive);
        serialized.ShouldContain("ScenarioP95SelectionSignalMs");
        serialized.ShouldNotContain("ObservedScenarioP95Ms");
    }

    [Theory]
    [InlineData("Host=ep-example.neon.tech;Database=integration_test;Username=test", "Test")]
    [InlineData("Host=localhost;Database=application;Username=test", "Test")]
    [InlineData("Host=localhost;Database=integration_test;Username=test", "Production")]
    public void ValidateTestOnlyConnection_ShouldRejectNonTestOrNeonTargets(string connectionString, string environment)
    {
        Should.Throw<InvalidOperationException>(() =>
            PostgreSqlReadPlanAnalysis.ValidateTestOnlyConnection(connectionString, environment));
    }
}

[Collection(nameof(IntegrationTestCollection))]
public sealed class PostgreSqlReadPlanAnalysisIntegrationTests(IntegrationTestWebAppFactory factory)
{
    [ExplicitPostgreSqlReadPlanAnalysisFact]
    public async Task ReadOnlyTransaction_ShouldRejectWriteCommandsAndExposeReadOnlyState()
    {
        await using var connection = new Npgsql.NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync();

        string state = await PostgreSqlReadPlanAnalysis.ExecuteReadOnlyTransactionAsync(connection, async (transaction, token) =>
        {
            await using var command = new Npgsql.NpgsqlCommand("SHOW transaction_read_only", connection, transaction);
            return (string)(await command.ExecuteScalarAsync(token))!;
        });
        state.ShouldBe("on");

        Npgsql.PostgresException exception = await Should.ThrowAsync<Npgsql.PostgresException>(() =>
            PostgreSqlReadPlanAnalysis.ExecuteReadOnlyTransactionAsync(connection, async (transaction, token) =>
            {
                await using var command = new Npgsql.NpgsqlCommand(
                    "UPDATE public.organizations SET name = name WHERE false", connection, transaction);
                await command.ExecuteNonQueryAsync(token);
                return 0;
            }));
        exception.SqlState.ShouldBe("25006");
    }

    [ExplicitPostgreSqlReadPlanAnalysisFact]
    [Trait("Category", "Performance")]
    [Trait("WorkloadClass", PerformanceWorkloadContracts.NormalExpectedTraffic)]
    public async Task QuickAnalysis_ShouldReadSmallSyntheticDatasetAndEmitOnlySafeEvidence()
    {
        var configuration = PostgreSqlReadPlanAnalysisConfiguration.FromEnvironment();
        string databaseName = new Npgsql.NpgsqlConnectionStringBuilder(factory.DatabaseConnectionString).Database
            ?? throw new InvalidOperationException("Integration test database name is required.");
        await SyntheticDatasetSeeder.SeedAsync(configuration.DatasetProfile,
            new SyntheticDatasetSeedOptions(factory.DatabaseConnectionString, databaseName, "Test", configuration.Seed));
        SyntheticDatasetManifest manifest = SyntheticDatasetManifestFactory.Create(configuration.DatasetProfile, configuration.Seed);
        IReadOnlyDictionary<ApiLoadTestScenario, ApiLoadTestScenarioMetrics> measurements = new Dictionary<ApiLoadTestScenario, ApiLoadTestScenarioMetrics>
        {
            [ApiLoadTestScenario.ReadList] = new(10, 10, 0, 0, 1, 10, 12),
            [ApiLoadTestScenario.ReadDetail] = new(10, 10, 0, 0, 1, 20, 25),
            [ApiLoadTestScenario.Report] = new(10, 10, 0, 0, 1, 5, 7)
        };

        IReadOnlyList<ReadPlanEvidence> evidence = await PostgreSqlReadPlanAnalysis.AnalyzeAsync(
            factory.DatabaseConnectionString, "Test", manifest, measurements);
        string serialized = JsonSerializer.Serialize(evidence);

        evidence.Count.ShouldBe(3);
        foreach (ReadPlanEvidence item in evidence)
        {
            (item.Plan.ExecutionTimeMs is not null && item.Plan.NodeTypes.Count > 0).ShouldBeTrue();
        }
        serialized.ShouldNotContain(manifest.GetOrganizationId(0).ToString("D"), Case.Insensitive);
        serialized.ShouldNotContain("FROM organizations", Case.Insensitive);
        serialized.ShouldNotContain("@organization_id", Case.Insensitive);
        serialized.ShouldNotContain("organizations WHERE", Case.Insensitive);
        serialized.ShouldContain("representative-read-component-plan");
        serialized.ShouldContain("not captured production SQL", Case.Insensitive);
        serialized.ShouldContain("ScenarioP95SelectionSignalMs");
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ExplicitPostgreSqlReadPlanAnalysisFactAttribute : FactAttribute
{
    public ExplicitPostgreSqlReadPlanAnalysisFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(PostgreSqlReadPlanAnalysis.GateEnvironmentVariable),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Explicit PostgreSQL plan analysis. Set RUN_POSTGRES_READ_PLAN_ANALYSIS=1; Medium additionally requires EIAMS_ALLOW_LONG_PLAN_ANALYSIS=1.";
        }
    }
}
