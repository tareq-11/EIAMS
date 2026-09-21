namespace IntegrationTests.Infrastructure;

[Collection(nameof(IntegrationTestCollection))]
public sealed class RbacV1PreflightSafetyTests
{
    private readonly IntegrationTestWebAppFactory factory;

    public RbacV1PreflightSafetyTests(IntegrationTestWebAppFactory factory) => this.factory = factory;

    [Fact]
    public void RbacV1Preflight_ShouldBeReadOnlyAndCheckAllPhase4Invariants()
    {
        string root = FindRepositoryRoot();
        string sql = File.ReadAllText(Path.Combine(root, "scripts", "rbac-v1-preflight.sql"));

        sql.ShouldContain("BEGIN TRANSACTION READ ONLY");
        sql.ShouldContain("ROLLBACK");
        sql.ShouldContain("legacy_permissions <> 38");
        sql.ShouldContain("dotted_permissions <> 29");
        sql.ShouldContain("mapping_pairs <> 42");
        sql.ShouldContain("legacy_scope_rows <> 77");
        sql.ShouldContain("dotted_scope_rows <> 56");
        sql.ShouldContain("target_grants <> 51");
        sql.ShouldContain("target_role_scopes <> 9");
        sql.ShouldContain("permission_code_mappings");
        sql.ShouldContain("dotted-v1");
        sql.ShouldContain("legacy-colon");
        sql.ShouldContain("effective parity report");
        sql.ShouldContain("dotted parity mismatch");
        sql.ShouldContain("mapping rows are intentionally absent");
        sql.ShouldNotContain("INSERT INTO");
        sql.ShouldNotContain("UPDATE ");
        sql.ShouldNotContain("DELETE ");
        sql.ShouldNotContain("CREATE ");
        sql.ShouldNotContain("DROP ");
    }

    [Fact]
    public async Task RbacV1PreflightRunner_ShouldHaveExplicitNonProductionGuardsAndValidShell()
    {
        string root = FindRepositoryRoot();
        string scriptPath = Path.Combine(root, "scripts", "run-rbac-v1-preflight.sh");
        string script = await File.ReadAllTextAsync(scriptPath);
        script.ShouldContain("RBAC_PREFLIGHT_NON_PRODUCTION");
        script.ShouldContain("RBAC_PREFLIGHT_DATABASE_URL");
        script.ShouldContain("neon");
        script.ShouldContain("production");
        script.ShouldContain("rbac-v1-preflight.sql");

        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo("bash")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };
        process.StartInfo.ArgumentList.Add("-n");
        process.StartInfo.ArgumentList.Add(scriptPath);
        process.Start();
        await process.WaitForExitAsync();
        process.ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task RbacV1PreflightSql_ShouldExecuteReadOnlyAgainstMigratedTestcontainer()
    {
        string sql = await File.ReadAllTextAsync(Path.Combine(FindRepositoryRoot(), "scripts", "rbac-v1-preflight.sql"));
        string sqlBody = string.Join('\n', sql.Split('\n').Where(line => !line.TrimStart().StartsWith('\\')));
        await using var connection = new Npgsql.NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync();
#pragma warning disable CA2100 // SQL is a checked-in read-only preflight artifact, not user input.
        await using var command = new Npgsql.NpgsqlCommand(sqlBody, connection);
#pragma warning restore CA2100

        await command.ExecuteNonQueryAsync();
        connection.State.ShouldBe(System.Data.ConnectionState.Open);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CleanArchitectureTemplate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
