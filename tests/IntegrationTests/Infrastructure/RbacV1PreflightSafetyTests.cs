namespace IntegrationTests.Infrastructure;

[Collection(nameof(IntegrationTestCollection))]
public sealed class RbacV1PreflightSafetyTests
{
    private readonly IntegrationTestWebAppFactory factory;

    public RbacV1PreflightSafetyTests(IntegrationTestWebAppFactory factory) => this.factory = factory;

    [Fact]
    public void RbacV2Preflight_ShouldBeReadOnlyAndCheckDottedCutoverInvariants()
    {
        string root = FindRepositoryRoot();
        string sql = File.ReadAllText(Path.Combine(root, "scripts", "rbac-v2-preflight.sql"));

        sql.ShouldContain("BEGIN TRANSACTION READ ONLY");
        sql.ShouldContain("COMMIT");
        sql.ShouldContain("legacy_permission_rows");
        sql.ShouldContain("unknown_permission_rows");
        sql.ShouldContain("missing_legacy_mappings");
        sql.ShouldContain("legacy_role_grant_parity");
        sql.ShouldContain("legacy_scope_parity");
        sql.ShouldContain("legacy_permission_rows' || '|' || CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'WARN'");
        sql.ShouldContain("dotted_grants_without_scope' || '|' || CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK'");
        sql.ShouldContain("dotted_grants_without_scope");
        sql.ShouldContain("permission_code_mappings");
        sql.ShouldContain("dotted-v1");
        sql.ShouldContain("dotted-v1");
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
        string scriptPath = Path.Combine(root, "scripts", "run-rbac-v2-preflight.sh");
        string script = await File.ReadAllTextAsync(scriptPath);
        script.ShouldContain("RBAC_V2_ALLOW_NON_PRODUCTION");
        script.ShouldContain("RBAC_V2_PGDATABASE");
        script.ShouldContain("neon");
        script.ShouldContain("production");
        script.ShouldContain("rbac-v2-preflight.sql");

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
        string sql = await File.ReadAllTextAsync(Path.Combine(FindRepositoryRoot(), "scripts", "rbac-v2-preflight.sql"));
        string sqlBody = string.Join('\n', sql.Split('\n').Where(line => !line.TrimStart().StartsWith('\\')));
        await using var connection = new Npgsql.NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync();
#pragma warning disable CA2100 // SQL is a checked-in read-only preflight artifact, not user input.
        await using var command = new Npgsql.NpgsqlCommand(sqlBody, connection);
#pragma warning restore CA2100

        await command.ExecuteNonQueryAsync();
        connection.State.ShouldBe(System.Data.ConnectionState.Open);
    }

    [Fact]
    public async Task RbacV2Preflight_ShouldReportWarnForManagedLegacyAndBlockUnknownRows()
    {
        await using var connection = new Npgsql.NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync();
        var legacyPermissionId = Guid.NewGuid();
        var unknownPermissionId = Guid.NewGuid();
        try
        {
            await ExecuteSqlAsync(connection, $"INSERT INTO public.permissions (id, code, name_ar, description) VALUES ('{legacyPermissionId}', 'users:access', 'صلاحية قديمة', 'preflight test legacy'), ('{unknownPermissionId}', 'unknown.preflight', 'صلاحية غير معروفة', 'preflight test unknown')");
            await ExecuteSqlAsync(connection, $"INSERT INTO public.role_permissions (permission_id, role_id) VALUES ('{legacyPermissionId}', '00000000-0000-0000-0000-000000000001')");
            await ExecuteSqlAsync(connection, $"INSERT INTO public.permission_allowed_scope_types (permission_id, scope_type) VALUES ('{legacyPermissionId}', 'Enterprise')");

            Dictionary<string, string> statuses = await ReadPreflightStatusesAsync(connection);
            statuses["legacy_permission_rows"].ShouldBe("WARN");
            statuses["legacy_role_grants"].ShouldBe("WARN");
            statuses["legacy_allowed_scope_rows"].ShouldBe("WARN");
            statuses["unknown_permission_rows"].ShouldBe("BLOCK");
            statuses["missing_legacy_mappings"].ShouldBe("PASS");
        }
        finally
        {
            await ExecuteSqlAsync(connection, $"DELETE FROM public.permission_allowed_scope_types WHERE permission_id IN ('{legacyPermissionId}', '{unknownPermissionId}')");
            await ExecuteSqlAsync(connection, $"DELETE FROM public.role_permissions WHERE permission_id IN ('{legacyPermissionId}', '{unknownPermissionId}')");
            await ExecuteSqlAsync(connection, $"DELETE FROM public.permissions WHERE id IN ('{legacyPermissionId}', '{unknownPermissionId}')");
        }
    }

    [Fact]
    public async Task RbacV2Preflight_ShouldReportPassForEverySectionOnLatestCutover()
    {
        await using var connection = new Npgsql.NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync();
        Dictionary<string, string> statuses = await ReadPreflightStatusesAsync(connection);
        statuses.Values.ShouldAllBe(status => status == "PASS");
    }

    private async Task<Dictionary<string, string>> ReadPreflightStatusesAsync(Npgsql.NpgsqlConnection connection)
    {
        string root = FindRepositoryRoot();
        string sql = await File.ReadAllTextAsync(Path.Combine(root, "scripts", "rbac-v2-preflight.sql"));
        Dictionary<string, string> statuses = new(StringComparer.Ordinal);
        await using Npgsql.NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await using (var readOnlyCommand = new Npgsql.NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
        {
            await readOnlyCommand.ExecuteNonQueryAsync();
        }

        foreach (string statement in sql.Split('\n').Where(line => line.StartsWith("SELECT ", StringComparison.Ordinal)))
        {
#pragma warning disable CA2100
            await using var command = new Npgsql.NpgsqlCommand(statement, connection, transaction);
#pragma warning restore CA2100
            await using Npgsql.NpgsqlDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string[] parts = reader.GetString(0).Split('|', 4);
                parts.Length.ShouldBeGreaterThanOrEqualTo(2);
                statuses[parts[0]] = parts[1];
            }
        }

        await transaction.CommitAsync();

        statuses.Count.ShouldBe(14);
        return statuses;
    }

    private static async Task ExecuteSqlAsync(Npgsql.NpgsqlConnection connection, string sql)
    {
#pragma warning disable CA2100
        await using var command = new Npgsql.NpgsqlCommand(sql, connection);
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public void DottedCutover_ShouldGuardMappingsAndParityBeforeDestructiveDeletes_AndNeverTranslateAtRuntime()
    {
        string root = FindRepositoryRoot();
        string migration = File.ReadAllText(Directory.GetFiles(Path.Combine(root, "src", "Infrastructure", "Migrations"), "20260922220000_CutoverToDottedOnlyPermissionVocabulary.cs").Single());
        int firstDelete = migration.IndexOf("DELETE FROM public.role_permissions", StringComparison.Ordinal);

        firstDelete.ShouldBeGreaterThan(0);
        migration.IndexOf("unknown permission code exists", StringComparison.Ordinal).ShouldBeLessThan(firstDelete);
        migration.IndexOf("mapping target is unknown or non-dotted", StringComparison.Ordinal).ShouldBeLessThan(firstDelete);
        migration.IndexOf("mapping source is unknown or ambiguous", StringComparison.Ordinal).ShouldBeLessThan(firstDelete);
        migration.IndexOf("legacy role grant parity is incomplete", StringComparison.Ordinal).ShouldBeLessThan(firstDelete);
        migration.IndexOf("legacy allowed-scope parity is incomplete", StringComparison.Ordinal).ShouldBeLessThan(firstDelete);

        string authorization = File.ReadAllText(Path.Combine(root, "src", "Infrastructure", "Authorization", "ScopeAuthorizationService.cs"));
        authorization.ShouldNotContain("LegacyColonCodes");
        authorization.ShouldContain("PermissionVocabulary.DottedV1Codes");
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
