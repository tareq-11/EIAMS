using System.Diagnostics;
using System.Text.Json;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// Verifies the staging release-evidence shell contract without contacting a deployment or a
/// database. The fake executables validate only the runner's safe orchestration shape.
/// </summary>
public sealed class StagingReleaseEvidenceSafetyTests
{
    [Fact]
    public void StagingReleaseEvidence_ShouldBeClosedByDefault_AndContainRequiredSafetyGates()
    {
        string script = File.ReadAllText(ScriptPath);

        script.ShouldContain("RUN_STAGING_RELEASE_EVIDENCE");
        script.ShouldContain("RUN_STAGING_RELEASE_MUTATIONS");
        script.ShouldContain("I_UNDERSTAND_THIS_CHANGES_APPROVED_STAGING");
        script.ShouldContain("distinct staging marker");
        script.ShouldContain("private or loopback address");
        script.ShouldContain("separate DB connection values");
        script.ShouldContain("proto = \"=https\"");
        script.ShouldContain("PGPASSWORD=\"$db_password\"");
        script.ShouldContain("PGSSLMODE=verify-full");
        script.ShouldContain("default_transaction_read_only=on");
        script.ShouldContain("EIAMS_STAGING_APPROVED_DB_HOST");
        script.ShouldContain("stock_movements");
        script.ShouldContain("asset_movement_history");
        script.ShouldContain("document_lifecycle_events");
        script.ShouldContain("WITH RECURSIVE inherited_roles");
        script.ShouldContain("NOT r.rolreplication");
        script.ShouldContain("(:[0-9]{1,5})?");
        script.ShouldContain("$((10#$api_port)) -ge 1");
        script.ShouldContain("$((10#$api_port)) -le 65535");
        script.ShouldContain("NOT EXISTS (SELECT 1 FROM inherited_roles)");
        script.ShouldContain("'public.audit_logs', 'SELECT') AND has_table_privilege(current_user, 'public.audit_logs', 'INSERT')");
        script.ShouldContain("NOT has_table_privilege(current_user, 'public.audit_logs', 'UPDATE') AND NOT has_table_privilege(current_user, 'public.audit_logs', 'DELETE')");
        script.ShouldContain("'public.stock_movements', 'SELECT') AND has_table_privilege(current_user, 'public.stock_movements', 'INSERT')");
        script.ShouldContain("'public.asset_movement_history', 'SELECT') AND has_table_privilege(current_user, 'public.asset_movement_history', 'INSERT')");
        script.ShouldContain("'public.document_lifecycle_events', 'SELECT') AND has_table_privilege(current_user, 'public.document_lifecycle_events', 'INSERT')");
        script.ShouldContain("'public.\"__EFMigrationsHistory\"', 'INSERT')");
        script.ShouldNotContain("'SELECT,INSERT'");
        script.ShouldNotContain("'UPDATE,DELETE'");
        script.ShouldContain("__EFMigrationsHistory");
        script.ShouldContain("audit_logs");
        script.ShouldContain("audit_log_entries");
        script.ShouldContain("Idempotency-Key");
        script.ShouldContain("proxy behavior was not observed");
        script.ShouldContain("dedicated temporary staging evidence subtree");
        script.ShouldContain("mv -f -- \"$temporary\" \"$target\"");
        script.ShouldNotContain("psql \"$database_url\"");
        script.ShouldNotContain("--insecure");
        script.ShouldNotContain(" -k");
    }

    [Fact]
    public async Task StagingReleaseEvidence_ShouldPassBashSyntaxValidation()
    {
        ProcessResult result = await RunAsync("bash", ["-n", ScriptPath]);

        result.ExitCode.ShouldBe(0, result.StandardError);
    }

    [Fact]
    public async Task StagingReleaseEvidence_ShouldRefuseWhenNotExplicitlyEnabled()
    {
        ProcessResult result = await RunAsync(ScriptPath);

        result.ExitCode.ShouldBe(3);
        result.StandardError.ShouldContain("Refusing staging release evidence");
    }

    #pragma warning disable CA1054 // Inline test data exercises rejected URL strings.
    [Theory]
    [InlineData("https://api.prod.example.test", "api.prod.example.test", "db.staging.example.test", "db.staging.example.test")]
    [InlineData("https://api.staging.example.test", "api.staging.example.test", "db.staging.example.test", "other.staging.example.test")]
    [InlineData("https://api.staging.example.test:0", "api.staging.example.test", "db.staging.example.test", "db.staging.example.test")]
    [InlineData("https://api.staging.example.test:65536", "api.staging.example.test", "db.staging.example.test", "db.staging.example.test")]
    public async Task StagingReleaseEvidence_ShouldRefuseUnsafeOrMismatchedApprovedHosts(
        string apiUrl, string apiHost, string dbHost, string approvedDbHost)
    {
        var environment = new Dictionary<string, string?>
        {
            ["RUN_STAGING_RELEASE_EVIDENCE"] = "1",
            ["EIAMS_STAGING_API_URL"] = apiUrl,
            ["EIAMS_STAGING_APPROVED_HOST"] = apiHost,
            ["EIAMS_STAGING_DB_HOST"] = dbHost,
            ["EIAMS_STAGING_APPROVED_DB_HOST"] = approvedDbHost,
            ["EIAMS_STAGING_DB_NAME"] = "db",
            ["EIAMS_STAGING_DB_USER"] = "user",
            ["EIAMS_STAGING_DB_PASSWORD"] = "secret",
            ["EIAMS_STAGING_EVIDENCE_DIR"] = Path.Combine(Path.GetTempPath(), "wrong-evidence")
        };

        ProcessResult result = await RunAsync(ScriptPath, environment: environment);

        result.ExitCode.ShouldBe(3);
        result.StandardError.ShouldContain("Refusing staging release evidence");
    }
    #pragma warning restore CA1054

    [ShellJsonContractFact]
    public async Task StagingReleaseEvidence_ShouldUseFakeCommandsAndWriteOnlySanitizedReadOnlyEvidence()
    {
        string root = Path.Combine(Path.GetTempPath(), $"eiams-staging-evidence-test-{Guid.NewGuid():N}");
        string fakeBin = Path.Combine(root, "bin");
        string evidenceDirectory = Path.Combine(root, "eiams-staging-evidence");
        Directory.CreateDirectory(fakeBin);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(fakeBin, "getent"), "#!/usr/bin/env bash\nif [[ \"$*\" == *private.staging* ]]; then printf '127.0.0.1 STREAM host\\n'; else printf '203.0.113.10 STREAM host\\n'; fi\n");
            await File.WriteAllTextAsync(Path.Combine(fakeBin, "psql"), "#!/usr/bin/env bash\n[[ \"$*\" != *secret* ]] && [[ \"$PGPASSWORD\" == db-secret ]] && [[ \"$PGSSLMODE\" == verify-full ]] && [[ \"$PGOPTIONS\" == *default_transaction_read_only* ]] && [[ -z \"${PGSSLROOTCERT+x}\" ]] || exit 8\ncase \"${EIAMS_FAKE_PSQL_MODE:-pass}\" in unavailable) exit 9 ;; fail) printf 'fail\\n' ;; *) printf 'pass\\n' ;; esac\n");
            await File.WriteAllTextAsync(Path.Combine(fakeBin, "curl"), """
#!/usr/bin/env bash
set -euo pipefail
config=""
while [[ $# -gt 0 ]]; do
  if [[ "$1" == "--config" ]]; then config="$2"; shift 2; else shift; fi
done
url="$(awk -F'"' '/^url = / { print $2; exit }' "$config")"
output="$(awk -F'"' '/^output = / { print $2; exit }' "$config")"
status=200
payload='{"success":true,"data":{}}'
case "$url" in
  */adjustments/*/post) payload='{"success":true,"data":{}}'; touch "$(dirname "$0")/posted" ;;
  */adjustments/*/reverse) if [[ "${EIAMS_FAKE_CURL_MODE:-}" == bad_reversal ]]; then payload='{"success":true,"data":{"id":"../../unsafe"}}'; else payload='{"success":true,"data":{"id":"33333333-3333-4333-8333-333333333333"}}'; fi; touch "$(dirname "$0")/reversed"; status=201 ;;
  */adjustments/*) if [[ -f "$(dirname "$0")/posted" ]]; then payload='{"success":true,"data":{"document_status":"Posted","row_version":2}}'; else payload='{"success":true,"data":{"document_status":"Submitted","row_version":1}}'; fi ;;
  */warehouse-documents/*) payload='{"success":true,"data":{}}' ;;
  */health|*/health/live|*/health/ready) payload='{"status":"Healthy"}' ;;
  */metrics|*/debug|*/diagnostics) status=404; payload='{"success":false,"error":{}}' ;;
  */auth/login) if [[ "${EIAMS_FAKE_CURL_MODE:-}" == bad_token ]]; then payload='{"success":true,"data":{"access_token":"unsafe-token-abcdefghijklmnopqrst"}}'; else payload='{"success":true,"data":{"access_token":"eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.signature"}}'; fi ;;
  */admin/users*|*/reports/inventory*) payload='{"success":true,"data":[]}' ;;
esac
printf '%s' "$payload" > "$output"
printf '%s\t%s' "$status" '0.001'
""");
            foreach (string tool in new[] { "getent", "psql", "curl" })
            {
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(Path.Combine(fakeBin, tool), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
            }

            var environment = new Dictionary<string, string?>
            {
                ["RUN_STAGING_RELEASE_EVIDENCE"] = "1",
                ["EIAMS_STAGING_API_URL"] = "https://api.staging.example.test",
                ["EIAMS_STAGING_APPROVED_HOST"] = "api.staging.example.test",
                ["EIAMS_STAGING_DB_HOST"] = "db.staging.example.test",
                ["EIAMS_STAGING_APPROVED_DB_HOST"] = "DB.STAGING.EXAMPLE.TEST",
                ["EIAMS_STAGING_DB_NAME"] = "eiams",
                ["EIAMS_STAGING_DB_USER"] = "runtime",
                ["EIAMS_STAGING_DB_PASSWORD"] = "db-secret",
                ["EIAMS_STAGING_EVIDENCE_DIR"] = evidenceDirectory,
                ["EIAMS_STAGING_ADMIN_EMAIL"] = "admin@example.test",
                ["EIAMS_STAGING_ADMIN_PASSWORD"] = "not-in-evidence",
                ["TMPDIR"] = root,
                ["PATH"] = $"{fakeBin}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}"
            };
            ProcessResult result = await RunAsync(ScriptPath, environment: environment);

            string diagnostic = Directory.Exists(evidenceDirectory)
                ? string.Join(Environment.NewLine, Directory.GetFiles(evidenceDirectory, "*.json").Select(File.ReadAllText))
                : string.Empty;
            result.ExitCode.ShouldBe(0, result.StandardError + result.StandardOutput + diagnostic);
            string evidencePath = Directory.GetFiles(evidenceDirectory, "*.json").Single();
            string evidence = await File.ReadAllTextAsync(evidencePath);
            using var document = JsonDocument.Parse(evidence);
            document.RootElement.GetProperty("mode").GetString().ShouldBe("read_only_preflight");
            document.RootElement.GetProperty("status").GetString().ShouldBe("preflight_passed");
            document.RootElement.GetProperty("releaseGateSatisfied").GetBoolean().ShouldBeFalse();
            document.RootElement.GetProperty("mutationsRequested").GetBoolean().ShouldBeFalse();
            evidence.ShouldNotContain("api.staging.example.test");
            evidence.ShouldNotContain("runtime:secret");
            evidence.ShouldNotContain("eyJhbGciOiJIUzI1NiJ9");
            evidence.ShouldNotContain("admin@example.test");
            evidence.ShouldNotContain("SELECT ");

            var privateDnsEnvironment = new Dictionary<string, string?>(environment)
            {
                ["EIAMS_STAGING_API_URL"] = "https://private.staging.example.test",
                ["EIAMS_STAGING_APPROVED_HOST"] = "private.staging.example.test"
            };
            ProcessResult privateDns = await RunAsync(ScriptPath, environment: privateDnsEnvironment);
            privateDns.ExitCode.ShouldBe(3);
            privateDns.StandardError.ShouldContain("private or loopback");

            var wrongEvidenceEnvironment = new Dictionary<string, string?>(environment)
            {
                ["EIAMS_STAGING_EVIDENCE_DIR"] = Path.Combine(root, "not-the-dedicated-evidence-directory")
            };
            Directory.CreateDirectory(wrongEvidenceEnvironment["EIAMS_STAGING_EVIDENCE_DIR"]!);
            ProcessResult wrongEvidence = await RunAsync(ScriptPath, environment: wrongEvidenceEnvironment);
            wrongEvidence.ExitCode.ShouldBe(3);
            wrongEvidence.StandardError.ShouldContain("dedicated temporary staging evidence subtree");

            var unconfirmedMutationEnvironment = new Dictionary<string, string?>(environment)
            {
                ["RUN_STAGING_RELEASE_MUTATIONS"] = "1",
                ["EIAMS_STAGING_DISPOSABLE_SUBMITTED_ADJUSTMENT_ID"] = "11111111-1111-4111-8111-111111111111",
                ["EIAMS_STAGING_POST_IDEMPOTENCY_KEY"] = "22222222-2222-4222-8222-222222222222",
                ["EIAMS_STAGING_REVERSE_IDEMPOTENCY_KEY"] = "44444444-4444-4444-8444-444444444444"
            };
            ProcessResult unconfirmedMutation = await RunAsync(ScriptPath, environment: unconfirmedMutationEnvironment);
            unconfirmedMutation.ExitCode.ShouldBe(4);
            string failedEvidence = await File.ReadAllTextAsync(Directory.GetFiles(evidenceDirectory, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).First());
            failedEvidence.ShouldContain("mutation_authorization");
            failedEvidence.ShouldContain("confirmation_required");
            using var failedDocument = JsonDocument.Parse(failedEvidence);
            failedDocument.RootElement.GetProperty("status").GetString().ShouldBe("failed");
            failedDocument.RootElement.GetProperty("releaseGateSatisfied").GetBoolean().ShouldBeFalse();
            failedEvidence.ShouldNotContain("11111111-1111-4111-8111-111111111111");
            failedEvidence.ShouldNotContain("not-in-evidence");

            foreach ((string psqlMode, string outcome) in new[] { ("unavailable", "unavailable"), ("fail", "privileges") })
            {
                var psqlFailureEnvironment = new Dictionary<string, string?>(environment) { ["EIAMS_FAKE_PSQL_MODE"] = psqlMode };
                ProcessResult psqlFailure = await RunAsync(ScriptPath, environment: psqlFailureEnvironment);
                psqlFailure.ExitCode.ShouldBe(4);
                string psqlEvidence = await File.ReadAllTextAsync(Directory.GetFiles(evidenceDirectory, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).First());
                psqlEvidence.ShouldContain("database_runtime_role");
                psqlEvidence.ShouldContain(outcome);
            }

            var badTokenEnvironment = new Dictionary<string, string?>(environment) { ["EIAMS_FAKE_CURL_MODE"] = "bad_token" };
            ProcessResult badToken = await RunAsync(ScriptPath, environment: badTokenEnvironment);
            badToken.ExitCode.ShouldBe(4);
            string badTokenEvidence = await File.ReadAllTextAsync(Directory.GetFiles(evidenceDirectory, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).First());
            badTokenEvidence.ShouldContain("unsafe_token");
            badTokenEvidence.ShouldNotContain("authenticated_session");

            const string adjustmentId = "11111111-1111-4111-8111-111111111111";
            const string postKey = "22222222-2222-4222-8222-222222222222";
            const string reverseKey = "44444444-4444-4444-8444-444444444444";
            environment["RUN_STAGING_RELEASE_MUTATIONS"] = "1";
            environment["EIAMS_STAGING_MUTATION_CONFIRMATION"] = "I_UNDERSTAND_THIS_CHANGES_APPROVED_STAGING";
            environment["EIAMS_STAGING_DISPOSABLE_SUBMITTED_ADJUSTMENT_ID"] = adjustmentId;
            environment["EIAMS_STAGING_POST_IDEMPOTENCY_KEY"] = postKey;
            environment["EIAMS_STAGING_REVERSE_IDEMPOTENCY_KEY"] = reverseKey;
            ProcessResult mutation = await RunAsync(ScriptPath, environment: environment);
            mutation.ExitCode.ShouldBe(0, mutation.StandardError);
            string mutationEvidence = await File.ReadAllTextAsync(Directory.GetFiles(evidenceDirectory, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).First());
            mutationEvidence.ShouldContain("adjustment_post");
            mutationEvidence.ShouldContain("adjustment_reverse");
            mutationEvidence.ShouldContain("reversal_detail");
            using var mutationDocument = JsonDocument.Parse(mutationEvidence);
            mutationDocument.RootElement.GetProperty("mode").GetString().ShouldBe("full_staging_smoke");
            mutationDocument.RootElement.GetProperty("status").GetString().ShouldBe("passed");
            mutationDocument.RootElement.GetProperty("releaseGateSatisfied").GetBoolean().ShouldBeTrue();
            mutationEvidence.ShouldNotContain(adjustmentId);
            mutationEvidence.ShouldNotContain(postKey);
            mutationEvidence.ShouldNotContain(reverseKey);
            mutationEvidence.ShouldNotContain("eyJhbGciOiJIUzI1NiJ9");

            File.Delete(Path.Combine(fakeBin, "posted"));
            var badReversalEnvironment = new Dictionary<string, string?>(environment)
            {
                ["RUN_STAGING_RELEASE_MUTATIONS"] = "1",
                ["EIAMS_STAGING_MUTATION_CONFIRMATION"] = "I_UNDERSTAND_THIS_CHANGES_APPROVED_STAGING",
                ["EIAMS_STAGING_DISPOSABLE_SUBMITTED_ADJUSTMENT_ID"] = adjustmentId,
                ["EIAMS_STAGING_POST_IDEMPOTENCY_KEY"] = postKey,
                ["EIAMS_STAGING_REVERSE_IDEMPOTENCY_KEY"] = reverseKey,
                ["EIAMS_FAKE_CURL_MODE"] = "bad_reversal"
            };
            ProcessResult badReversal = await RunAsync(ScriptPath, environment: badReversalEnvironment);
            badReversal.ExitCode.ShouldBe(4);
            string badReversalEvidence = await File.ReadAllTextAsync(Directory.GetFiles(evidenceDirectory, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).First());
            badReversalEvidence.ShouldContain("adjustment_reverse");
            badReversalEvidence.ShouldNotContain("reversal_detail");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static string ScriptPath => Path.Combine(FindRepositoryRoot(), "scripts", "run-staging-release-evidence.sh");

    private static async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string>? arguments = null,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        ProcessStartInfo startInfo = ShellScriptProcess.Create(fileName, arguments);

        foreach ((string name, string? value) in environment ?? new Dictionary<string, string?>())
        {
            startInfo.Environment[name] = value;
        }

        using var process = Process.Start(startInfo);
        process.ShouldNotBeNull();
        await process!.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        return new ProcessResult(process.ExitCode, await process.StandardOutput.ReadToEndAsync(), await process.StandardError.ReadToEndAsync());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CleanArchitectureTemplate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class ShellJsonContractFactAttribute : FactAttribute
{
    public ShellJsonContractFactAttribute()
    {
        if (OperatingSystem.IsWindows() && !ShellScriptProcess.IsNativeCommandAvailable("jq.exe"))
        {
            Skip = "The shell JSON contract test requires jq. Install jq on Windows or run it on the Linux CI host.";
        }
    }
}
