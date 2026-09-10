using System.Diagnostics;
using System.Security.Cryptography;
using Application.Abstractions.Storage;
using Domain.AuditLogs;
using Domain.Common;
using Domain.DocumentAttachments;
using Domain.Organizations;
using Domain.Sites;
using Domain.Users;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Infrastructure.Database;
using Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using SharedKernel;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// Contract tests for the explicitly opt-in shell rehearsal. The destructive portion is never run
/// by the integration suite; it needs a separately supplied disposable Testcontainers URL.
/// </summary>
public sealed class BackupRestoreRehearsalSafetyTests
{
    [Fact]
    public void BackupRestoreRehearsal_ShouldBeOffByDefault_LocalOnly_AndRedactItsEvidence()
    {
        string script = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "backup-restore-rehearsal.sh"));

        script.ShouldContain("RUN_LOCAL_BACKUP_RESTORE_REHEARSAL");
        script.ShouldContain("clean_architecture_integration_test");
        script.ShouldContain("clean_architecture_restore_");
        script.ShouldContain("localhost|127");
        script.ShouldContain("Neon is never permitted");
        script.ShouldContain("pg_dump");
        script.ShouldContain("pg_restore");
        script.ShouldContain("PGPASSWORD");
        script.ShouldContain("source is not quiesced");
        script.ShouldContain("stableBeforeAndAfterCapture");
        script.ShouldContain("potentialDataLossSeconds");
        script.ShouldContain("recoveryTimeSeconds");
        script.ShouldContain("dropdb --if-exists --force");
        script.ShouldContain("rm -rf -- \"$run_dir\"");
        script.ShouldContain("missingFiles\":0");
        script.ShouldContain("orphanFiles\":0");
        script.ShouldContain("checksumMismatches\":0");
        script.ShouldContain("No production, Neon, secrets, identifiers, storage keys, or connection strings were recorded.");
        script.ShouldContain("Audit logs remain append-only with no automatic deletion");
        script.ShouldContain("no business retention value is inferred");
    }

    [Fact]
    public async Task BackupRestoreRehearsal_ShouldPassBashSyntaxValidation()
    {
        string scriptPath = Path.Combine(FindRepositoryRoot(), "scripts", "backup-restore-rehearsal.sh");
        var startInfo = new ProcessStartInfo("bash")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-n");
        startInfo.ArgumentList.Add(scriptPath);
        using var process = Process.Start(startInfo);

        process.ShouldNotBeNull();
        await process!.WaitForExitAsync();
        string error = await process.StandardError.ReadToEndAsync();
        process.ExitCode.ShouldBe(0, error);
    }

    [ExplicitBackupRestoreRehearsalFact]
    public async Task BackupRestoreRehearsal_ShouldExecuteOnlyWhenExplicitlyOptedInAgainstFreshTestcontainers()
    {
        string repositoryRoot = FindRepositoryRoot();
        string evidenceDirectory = Path.Combine(Path.GetTempPath(), $"eiams-backup-restore-evidence-{Guid.NewGuid():N}");
        string sourceRole = $"rehearsal_reader_{Guid.NewGuid():N}";
        const string sourcePassword = "rehearsal-reader-password";
        var factory = new IntegrationTestWebAppFactory();
        await factory.InitializeAsync();
        try
        {
            await SeedAttachmentAndAuditAsync(factory);
            await CreateReadOnlySourceRoleAsync(factory, sourceRole);
            NpgsqlConnection.ClearAllPools();

            var source = new NpgsqlConnectionStringBuilder(factory.DatabaseConnectionString);
            string sourceUri = $"postgresql://{sourceRole}:{sourcePassword}@localhost:{source.Port}/clean_architecture_integration_test";
            string adminUri = $"postgresql://postgres:postgres@localhost:{source.Port}/postgres";
            string root = factory.Services.GetRequiredService<IOptions<LocalFileStorageOptions>>().Value.RootPath;
            var startInfo = new ProcessStartInfo(Path.Combine(repositoryRoot, "scripts", "backup-restore-rehearsal.sh"))
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            startInfo.Environment["RUN_LOCAL_BACKUP_RESTORE_REHEARSAL"] = "1";
            startInfo.Environment["EIAMS_REHEARSAL_SOURCE_DATABASE_URL"] = sourceUri;
            startInfo.Environment["EIAMS_REHEARSAL_ADMIN_DATABASE_URL"] = adminUri;
            startInfo.Environment["EIAMS_REHEARSAL_ATTACHMENT_ROOT"] = root;
            startInfo.Environment["EIAMS_REHEARSAL_EVIDENCE_DIR"] = evidenceDirectory;
            startInfo.Environment["EIAMS_REHEARSAL_RPO_OBJECTIVE_SECONDS"] = "300";
            startInfo.Environment["EIAMS_REHEARSAL_RTO_OBJECTIVE_SECONDS"] = "300";

            using var process = Process.Start(startInfo);
            process.ShouldNotBeNull();
            await process!.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(90));
            string error = await process.StandardError.ReadToEndAsync();
            process.ExitCode.ShouldBe(0, error);

            string evidenceFile = Directory.GetFiles(evidenceDirectory, "*.json").Single();
            string evidence = await File.ReadAllTextAsync(evidenceFile);
            evidence.ShouldContain("\"status\":\"pass\"");
            evidence.ShouldContain("\"stableBeforeAndAfterCapture\":true");
            evidence.ShouldNotContain("postgresql://", Case.Insensitive);
            evidence.ShouldNotContain("clean_architecture_restore_", Case.Insensitive);
        }
        finally
        {
            await DropRoleAsync(factory, sourceRole);
            if (Directory.Exists(evidenceDirectory))
            {
                Directory.Delete(evidenceDirectory, recursive: true);
            }

            await factory.DisposeAsync();
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The role identifier is generated as a Guid hexadecimal suffix inside this disposable Testcontainers-only test.")]
    private static async Task CreateReadOnlySourceRoleAsync(
        IntegrationTestWebAppFactory factory,
        string role)
    {
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(factory.DatabaseConnectionString)
        {
            Pooling = false
        }.ConnectionString);
        await connection.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE ROLE \"{role}\" LOGIN PASSWORD 'rehearsal-reader-password'", connection))
        {
            await create.ExecuteNonQueryAsync();
        }

        foreach (string sql in new[]
                 {
                     $"GRANT CONNECT ON DATABASE clean_architecture_integration_test TO \"{role}\"",
                     $"GRANT USAGE ON SCHEMA public TO \"{role}\"",
                     $"GRANT SELECT ON ALL TABLES IN SCHEMA public TO \"{role}\"",
                     $"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO \"{role}\""
                 })
        {
            await using var grant = new NpgsqlCommand(sql, connection);
            await grant.ExecuteNonQueryAsync();
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The role identifier is generated as a Guid hexadecimal suffix inside this disposable Testcontainers-only test.")]
    private static async Task DropRoleAsync(IntegrationTestWebAppFactory factory, string role)
    {
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(factory.DatabaseConnectionString)
        {
            Pooling = false
        }.ConnectionString);
        await connection.OpenAsync();
        await using (var revoke = new NpgsqlCommand($"DROP OWNED BY \"{role}\"", connection))
        {
            await revoke.ExecuteNonQueryAsync();
        }

        await using var drop = new NpgsqlCommand($"DROP ROLE IF EXISTS \"{role}\"", connection);
        await drop.ExecuteNonQueryAsync();
    }

    private static async Task SeedAttachmentAndAuditAsync(IntegrationTestWebAppFactory factory)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        User user = await context.Users.SingleAsync(x => x.Email == IntegrationTestWebAppFactory.AdministratorEmail);
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var organization = Organization.Create(Guid.NewGuid(), $"Rehearsal {suffix}", $"R{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Site {suffix}", $"S{suffix}", null);
        var warehouse = Warehouse.Create(Guid.NewGuid(), site.Id, $"Warehouse {suffix}", $"W{suffix}", "General", true);
        var document = WarehouseDocument.CreateDraft(Guid.NewGuid(), warehouse.Id, DocumentType.Receiving, $"rehearsal-{suffix}");
        context.AddRange(organization, site, warehouse, document);
        await context.SaveChangesAsync();

        byte[] content = "backup-restore-rehearsal-attachment"u8.ToArray();
        IFileStorage storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        await using var stream = new MemoryStream(content);
        Result<StoredFile> stored = await storage.SaveAsync(stream, CancellationToken.None);
        stored.IsSuccess.ShouldBeTrue();
        context.DocumentAttachments.Add(DocumentAttachment.Create(
            Guid.NewGuid(), document.Id, AttachmentType.Supporting, stored.Value.StorageKey, "rehearsal.txt", "text/plain",
            stored.Value.FileSize, stored.Value.Checksum, user.Id, DateTime.UtcNow));
        Result<AuditLog> audit = AuditLog.Create(Guid.NewGuid(), user.Id, $"rehearsal-{suffix}", null, "WarehouseDocument", document.Id,
            null, null, AuditActions.Create, null, "{\"source\":\"backup-restore-rehearsal\"}", null, DateTime.UtcNow);
        audit.IsSuccess.ShouldBeTrue();
        context.AuditLogs.Add(audit.Value);
        await context.SaveChangesAsync();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CleanArchitectureTemplate.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ExplicitBackupRestoreRehearsalFactAttribute : FactAttribute
{
    public ExplicitBackupRestoreRehearsalFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_BACKUP_RESTORE_REHEARSAL_INTEGRATION_TEST"), "1", StringComparison.Ordinal))
        {
            Skip = "Explicit local Testcontainers backup/restore rehearsal. Set RUN_BACKUP_RESTORE_REHEARSAL_INTEGRATION_TEST=1.";
        }
    }
}
