using System.Diagnostics;
using System.Text.Json;

namespace IntegrationTests.Infrastructure;

public sealed class Phase0BaselineSafetyTests
{
    [Fact]
    public void Phase0ScriptsAreExplicitlyReadOnlyAndDoNotMutateSchemaOrData()
    {
        string root = FindRepositoryRoot();
        string sql = File.ReadAllText(Path.Combine(root, "scripts", "phase0-baseline-preflight.sql"));
        string shell = File.ReadAllText(Path.Combine(root, "scripts", "run-phase0-baseline.sh"));

        sql.ShouldContain("BEGIN TRANSACTION READ ONLY");
        sql.ShouldContain("COMMIT");
        shell.ShouldContain("default_transaction_read_only=on");
        shell.ShouldContain("ON_ERROR_STOP=1");
        shell.ShouldNotContain("EIAMS_BASELINE_DATABASE_URL");
        shell.ShouldNotContain("psql \"$EIAMS_BASELINE_");
        shell.ShouldContain("PGDATABASE=\"$EIAMS_BASELINE_PGDATABASE\"");
        shell.ShouldContain("expected_db_sections=(");
        shell.ShouldContain("valid_db_output=false");
        shell.ShouldContain("any(.[]; .status == \"BLOCK\")");
        shell.ShouldContain("any(.[]; .status == \"WARN\")");
        shell.ShouldContain("mktemp \"$output_dir/.phase0-baseline.");
        foreach (string forbidden in new[] { "CREATE TABLE", "ALTER TABLE", "DROP TABLE", "INSERT INTO", "UPDATE ", "DELETE FROM", "TRUNCATE ", "pg_restore", "dotnet ef database update" })
        {
            sql.ShouldNotContain(forbidden, Case.Insensitive);
            shell.ShouldNotContain(forbidden, Case.Insensitive);
        }
    }

    [Fact]
    public void CheckedInBaselineContainsDeterministicSanitizedShape()
    {
        string root = FindRepositoryRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "contracts", "baseline", "phase0-baseline.json")));
        JsonElement rootElement = document.RootElement;
        rootElement.GetProperty("version").GetString().ShouldBe("phase-0a-v1");
        rootElement.GetProperty("semantics").GetProperty("BLOCK").GetString().ShouldNotBeNullOrWhiteSpace();
        rootElement.GetProperty("database").GetProperty("status").GetString().ShouldBe("UNAVAILABLE");
        rootElement.ToString().ShouldNotContain("postgresql://", Case.Insensitive);
        rootElement.ToString().ShouldNotContain("\"password\"", Case.Insensitive);
        rootElement.ToString().ShouldNotContain("\"access_token\"", Case.Insensitive);
    }

    [Fact]
    public void BaselineContainsContractArraysAndEveryRequiredDatabaseCheck()
    {
        string root = FindRepositoryRoot();
        string shell = File.ReadAllText(Path.Combine(root, "scripts", "run-phase0-baseline.sh"));
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "contracts", "baseline", "phase0-baseline.json")));
        JsonElement inventory = document.RootElement.GetProperty("sourceInventory");
        AssertSortedUniqueNonEmpty(inventory.GetProperty("openApi").GetProperty("routes"), "path", "method");
        AssertSortedUniqueNonEmpty(inventory.GetProperty("openApi").GetProperty("schemaNames"));
        AssertSortedUniqueNonEmpty(inventory.GetProperty("permissionCodes").GetProperty("values"));

        string sql = File.ReadAllText(Path.Combine(root, "scripts", "phase0-baseline-preflight.sql"));
        sql.ShouldContain("FROM public.permission_allowed_scope_types\nWHERE scope_type = 'OrganizationalUnit'");
        string[] requiredChecks =
        [
            "users_without_exactly_one_assignment", "organizational_unit_user_assignments",
            "organizational_unit_role_allowed_scopes", "organizational_unit_permission_allowed_scopes",
            "username_null_or_blank", "username_noncanonical", "username_invalid_shape_or_length",
            "username_legacy_generated", "username_duplicate_normalized", "legacy_colon_permissions", "non_dotted_non_legacy_permissions",
            "material_family_missing_base_unit", "material_missing_or_invalid_base_unit",
            "conversion_base_unit_mismatch", "non_supplier_receiving_info", "document_invalid_row_version",
            "posted_document_missing_metadata", "in_progress_count_missing_membership",
            "invalid_document_sequence_facts", "duplicate_document_sequence_keys"
        ];
        foreach (string check in requiredChecks)
        {
            sql.ShouldContain($"'{check}'");
            shell.ShouldContain(check);
        }
    }

    [Fact]
    public void SourceOnlyGenerationIsByteIdenticalAndHasNoSecretLikeConnectionInput()
    {
        string root = FindRepositoryRoot();
        string first = Path.Combine(Path.GetTempPath(), $"phase0-first-{Guid.NewGuid():N}.json");
        string second = Path.Combine(Path.GetTempPath(), $"phase0-second-{Guid.NewGuid():N}.json");
        try
        {
            RunSourceOnly(root, first).ShouldBe(4);
            RunSourceOnly(root, second).ShouldBe(4);
            File.ReadAllBytes(first).ShouldBe(File.ReadAllBytes(second));
            string artifact = File.ReadAllText(first);
            artifact.ShouldNotContain("postgresql://", Case.Insensitive);
            artifact.ShouldNotContain("\"password\"", Case.Insensitive);
            artifact.ShouldNotContain("\"access_token\"", Case.Insensitive);
        }
        finally
        {
            File.Delete(first);
            File.Delete(second);
        }
    }

    [Fact]
    public void MixedCaseProductionDatabaseNameIsRefusedBeforePsql()
    {
        string root = FindRepositoryRoot();
        var startInfo = new ProcessStartInfo("bash", Path.Combine(root, "scripts", "run-phase0-baseline.sh"))
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.Environment["EIAMS_BASELINE_ALLOW_NON_PRODUCTION"] = "1";
        startInfo.Environment["EIAMS_BASELINE_PGHOST"] = "localhost";
        startInfo.Environment["EIAMS_BASELINE_PGDATABASE"] = "ProductionCopy";
        startInfo.Environment["EIAMS_BASELINE_PGUSER"] = "baseline";
        startInfo.Environment["EIAMS_BASELINE_OUTPUT"] = Path.Combine(Path.GetTempPath(), $"phase0-refused-{Guid.NewGuid():N}.json");
        using Process process = Process.Start(startInfo)!;
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.ShouldBe(3);
        standardError.ShouldContain("non-production");
    }

    [Fact]
    public void Phase1dPreflightAndMigrationAreReadOnlyAndFailClosed()
    {
        string root = FindRepositoryRoot();
        string sql = File.ReadAllText(Path.Combine(root, "scripts", "phase1d-cutover-preflight.sql"));
        string shell = File.ReadAllText(Path.Combine(root, "scripts", "run-phase1d-cutover-preflight.sh"));
        string migration = File.ReadAllText(Directory.GetFiles(Path.Combine(root, "src", "Infrastructure", "Migrations"), "*_CutoverUserAssignmentScopeVocabulary.cs").Single());

        sql.ShouldContain("BEGIN TRANSACTION READ ONLY");
        shell.ShouldContain("default_transaction_read_only=on");
        shell.ShouldContain("mv -f -- \"$artifact_tmp\" \"$output\"");
        shell.ShouldContain("db_status=UNAVAILABLE");
        foreach (string check in new[] { "organizational_unit_user_assignments", "organizational_unit_role_allowed_scopes", "organizational_unit_permission_allowed_scopes", "users_without_exactly_one_assignment", "invalid_assignment_scope_id", "role_scope_incompatibility" })
        {
            sql.ShouldContain($"'{check}'");
            shell.ShouldContain(check);
        }

        migration.IndexOf("1D cutover blocked", StringComparison.Ordinal).ShouldBeLessThan(migration.IndexOf("DELETE FROM public.permission_allowed_scope_types", StringComparison.Ordinal));
        migration.IndexOf("active users need one assignment", StringComparison.Ordinal).ShouldBeLessThan(migration.IndexOf("DELETE FROM public.permission_allowed_scope_types", StringComparison.Ordinal));
        migration.IndexOf("assignment scope id shape or target is invalid", StringComparison.Ordinal).ShouldBeLessThan(migration.IndexOf("DELETE FROM public.permission_allowed_scope_types", StringComparison.Ordinal));
        migration.IndexOf("assignment role/scope compatibility is invalid", StringComparison.Ordinal).ShouldBeLessThan(migration.IndexOf("DELETE FROM public.permission_allowed_scope_types", StringComparison.Ordinal));
        migration.ShouldContain("WHERE scope_type = 'OrganizationalUnit'");
        migration.ShouldContain("scope_type IN ('Site', 'Warehouse')");
        migration.ShouldNotContain("OrganizationalUnitId =");
        sql.ShouldContain("CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'WARN' END");
        sql.ShouldContain("planned migration-managed removal");
        sql.ShouldContain("organizational_unit_role_allowed_scopes' || '|' || CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'WARN' END");
        sql.ShouldContain("organizational_unit_permission_allowed_scopes' || '|' || CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'WARN' END");
        sql.ShouldContain("organizational_unit_user_assignments' || '|' || CASE WHEN count(*) = 0 THEN 'PASS' ELSE 'BLOCK' END");
        sql.ShouldContain("u.status = 'Active' AND count(s.id) = 0");
        string startupValidator = File.ReadAllText(Path.Combine(root, "src", "Infrastructure", "Authorization", "AuthorizationPolicyStartupValidator.cs"));
        startupValidator.ShouldContain("UserStatus.Active");
        startupValidator.ShouldContain("AssignmentCount > 1");
        startupValidator.ShouldNotContain("AssignmentCount != 1");
    }

    private static void AssertSortedUniqueNonEmpty(JsonElement values, params string[] objectKeys)
    {
        values.GetArrayLength().ShouldBeGreaterThan(0);
        string[] serialized = values.EnumerateArray()
            .Select(value => objectKeys.Length == 0
                ? value.GetString()!
                : string.Join("|", objectKeys.Select(key => value.GetProperty(key).GetString())))
            .ToArray();
        serialized.Distinct(StringComparer.Ordinal).Count().ShouldBe(serialized.Length);
        if (objectKeys.Length == 0)
        {
            serialized.ShouldBe(serialized.OrderBy(item => item, StringComparer.Ordinal).ToArray());
        }
        else
        {
            (string Path, string Method)[] entries = values.EnumerateArray()
                .Select(value => (value.GetProperty(objectKeys[0]).GetString()!, value.GetProperty(objectKeys[1]).GetString()!))
                .ToArray();
            entries.ShouldBe(entries.OrderBy(item => item.Path, StringComparer.Ordinal).ThenBy(item => item.Method, StringComparer.Ordinal).ToArray());
        }
    }

    private static int RunSourceOnly(string root, string output)
    {
        var startInfo = new ProcessStartInfo("bash", Path.Combine(root, "scripts", "run-phase0-baseline.sh"))
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.Environment.Remove("EIAMS_BASELINE_PGHOST");
        startInfo.Environment.Remove("EIAMS_BASELINE_PGDATABASE");
        startInfo.Environment.Remove("EIAMS_BASELINE_PGUSER");
        startInfo.Environment.Remove("EIAMS_BASELINE_PGPASSWORD");
        startInfo.Environment["EIAMS_BASELINE_OUTPUT"] = output;
        using Process process = Process.Start(startInfo)!;
        process.WaitForExit();
        return process.ExitCode;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CleanArchitectureTemplate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
