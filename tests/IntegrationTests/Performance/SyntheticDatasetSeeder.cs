using System.Security.Cryptography;
using System.Text;
using Application.Abstractions.Data;
using Domain.AuditLogs;
using Domain.Common;
using Domain.DocumentAttachments;
using Domain.DocumentLines;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.OrganizationalUnits;
using Domain.Organizations;
using Domain.Roles;
using Domain.Sites;
using Domain.StockMovements;
using Domain.UnitsOfMeasure;
using Domain.UserRoleScopes;
using Domain.Users;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SharedKernel;

namespace IntegrationTests.Performance;

/// <summary>
/// Explicit, test-only runner for the Small synthetic benchmark dataset. It deliberately accepts
/// a connection string only from its caller so it cannot be reached by normal application paths.
/// </summary>
internal static class SyntheticDatasetSeeder
{
    private const int BatchSize = 250;
    private const string GeneratorVersion = "synthetic-dataset-generator-v2";
    private const string DatasetSchemaVersion = "synthetic-dataset-schema-v1";
    private const string IdentityAlgorithmVersion = "sha256-guid-v1";
    private const string AdvisoryLockResource = "integration-tests:synthetic-dataset-seeder";
    private static readonly string[] IdentityKindLabels =
    [
        "organization",
        "site",
        "organizational-unit",
        "warehouse",
        "role",
        "user",
        "material",
        "movement",
        "audit",
        "unit",
        "material-domain",
        "material-category",
        "material-family",
        "user-role-scope",
        "document",
        "document-line",
        "attachment",
        "audit-operation"
    ];
    private static readonly DateTime SeedTimestampUtc = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    internal static async Task<SyntheticDatasetSeedResult> SeedSmallAsync(
        SyntheticDatasetSeedOptions options,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);

        SyntheticDatasetManifest manifest = SyntheticDatasetManifestFactory.Create(DatasetProfile.Small, options.Seed);
        string manifestHash = ComputeManifestHash(manifest);

        DbContextOptions<ApplicationDbContext> contextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                options.ConnectionString,
                npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, "public"))
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var context = new ApplicationDbContext(contextOptions, new NoOpDomainEventsDispatcher());
        await context.Database.MigrateAsync(cancellationToken);
        await context.Database.OpenConnectionAsync(cancellationToken);

        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await AcquireDatasetLockAsync(context, cancellationToken);
            await EnsureRunTableAsync(context, cancellationToken);
            string? existingHash = await GetRunHashAsync(context, manifest.Profile, cancellationToken);
            if (existingHash is not null)
            {
                if (!string.Equals(existingHash, manifestHash, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "A different synthetic dataset manifest is already registered for the Small profile. " +
                        "Use a fresh test database rather than mixing benchmark datasets.");
                }

                await transaction.CommitAsync(cancellationToken);
                context.DiscardPostCommitActions();
                return await GetResultAsync(context, manifest, wasAlreadySeeded: true, cancellationToken);
            }

            await SeedAsync(context, manifest, cancellationToken);
            await RegisterRunAsync(context, manifest, manifestHash, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            context.DiscardPostCommitActions();
            throw;
        }

        context.DiscardPostCommitActions();
        return await GetResultAsync(context, manifest, wasAlreadySeeded: false, cancellationToken);
    }

    private static async Task SeedAsync(
        ApplicationDbContext context,
        SyntheticDatasetManifest manifest,
        CancellationToken cancellationToken)
    {
        string organizationIdPrefix = manifest.GetOrganizationId(0).ToString("N")[..8];
        string prefix = $"syn-{manifest.Seed:x}-{organizationIdPrefix}";
        Guid unitId = DeterministicGuid(manifest, "unit", 0);
        Guid materialDomainId = DeterministicGuid(manifest, "material-domain", 0);
        Guid materialCategoryId = DeterministicGuid(manifest, "material-category", 0);
        Guid materialFamilyId = DeterministicGuid(manifest, "material-family", 0);

        context.Organizations.AddRange(Enumerable.Range(0, manifest.Definition.OrganizationCount)
            .Select(index => Organization.Create(
                manifest.GetOrganizationId(index),
                $"Synthetic organization {index + 1}",
                $"{prefix}-org-{index + 1}")));

        context.Sites.AddRange(manifest.Sites.Select((site, index) => Site.Create(
            site.SiteId,
            site.OrganizationId,
            $"Synthetic site {index + 1}",
            $"{prefix}-site-{index + 1}",
            location: "Synthetic test location")));

        context.OrganizationalUnits.AddRange(manifest.Sites.SelectMany((site, siteIndex) =>
            site.OrganizationalUnitIds.Select((unit, unitIndex) => OrganizationalUnit.Create(
                unit,
                site.SiteId,
                parentId: null,
                $"Synthetic unit {siteIndex + 1}-{unitIndex + 1}",
                "Benchmark"))));

        context.Warehouses.AddRange(manifest.Sites.SelectMany((site, siteIndex) =>
            site.Warehouses.Select((warehouse, warehouseIndex) => Warehouse.Create(
                warehouse.WarehouseId,
                site.SiteId,
                $"Synthetic warehouse {siteIndex + 1}-{warehouseIndex + 1}",
                $"{prefix}-wh-{siteIndex + 1}-{warehouseIndex + 1}",
                "Benchmark",
                canHoldStock: true,
                organizationalUnitId: warehouse.OrganizationalUnitId))));

        context.Roles.AddRange(manifest.RoleIds.Select((roleId, index) => Role.Create(
            roleId,
            $"SYN_{prefix}_{index + 1}",
            "Synthetic benchmark role.")));

        context.Users.AddRange(Enumerable.Range(0, manifest.Definition.UserCount).Select(index => User.Create(
            manifest.GetUserId(index),
            $"{prefix}-user-{index + 1}@synthetic.test",
            "Synthetic",
            $"User{index + 1}",
            "synthetic-dataset-not-for-login")));

        context.UserRoleScopes.AddRange(manifest.UserScopeAssignments.Select((assignment, index) =>
            UserRoleScope.Create(
                DeterministicGuid(manifest, "user-role-scope", index),
                assignment.UserId,
                assignment.RoleId,
                assignment.ScopeType,
                assignment.ScopeId)));

        context.UnitsOfMeasure.Add(UnitOfMeasure.Create(unitId, "Synthetic unit", "syn", "Quantity"));
        context.MaterialDomains.Add(MaterialDomain.Create(materialDomainId, "Synthetic domain", $"{prefix}-domain"));
        context.MaterialCategories.Add(MaterialCategory.Create(
            materialCategoryId,
            materialDomainId,
            parentCategoryId: null,
            "Synthetic category",
            $"{prefix}-category"));
        context.MaterialFamilies.Add(MaterialFamily.Create(
            materialFamilyId,
            materialCategoryId,
            "Synthetic family",
            $"{prefix}-family",
            unitId));

        context.Materials.AddRange(Enumerable.Range(0, manifest.Definition.MaterialCount).Select(index => Material.Create(
            manifest.GetMaterialId(index),
            materialFamilyId,
            unitId,
            $"Synthetic material {index + 1}",
            $"Synthetic material {index + 1}",
            $"{prefix}-mat-{index + 1}",
            MaterialKind.Consumable,
            TrackingType.Quantity,
            hasExpiry: false,
            attributes: "{\"dataset\":\"synthetic\"}")));

        await context.SaveChangesAsync(cancellationToken);

        List<WarehouseDocument> documents = new(manifest.Definition.OperationalMovementCount);
        var lines = new List<DocumentLine>(manifest.Definition.OperationalMovementCount);
        SyntheticDatasetWarehouse[] warehouses = manifest.Sites.SelectMany(site => site.Warehouses).ToArray();
        for (int index = 0; index < manifest.Definition.OperationalMovementCount; index++)
        {
            Guid documentId = DeterministicGuid(manifest, "document", index);
            Guid materialId = manifest.GetMaterialId(index % manifest.Definition.MaterialCount);
            var document = WarehouseDocument.CreateDraft(
                documentId,
                warehouses[index % warehouses.Length].WarehouseId,
                DocumentType.Receiving,
                $"{prefix}-doc-{index + 1}");
            EnsureSuccess(document.UpdatePaperReference($"SYN-{index + 1}", 2025));
            documents.Add(document);
            lines.Add(DocumentLine.Create(
                DeterministicGuid(manifest, "document-line", index),
                documentId,
                materialId,
                DocumentLineType.Normal,
                quantity: 1m,
                unitId,
                baseQuantity: 1m,
                unitPrice: 1m,
                batchNumber: null,
                expiryDate: null).Value);
        }

        await SaveInBatchesAsync(context.WarehouseDocuments, documents, context, cancellationToken);
        await SaveInBatchesAsync(context.DocumentLines, lines, context, cancellationToken);

        var attachments = documents.Select((document, index) => DocumentAttachment.Create(
            DeterministicGuid(manifest, "attachment", index),
            document.Id,
            AttachmentType.SignedOriginal,
            $"synthetic/{manifest.Profile.ToString().ToUpperInvariant()}/{manifest.Seed}/{index + 1}.pdf",
            "synthetic.pdf",
            "application/pdf",
            fileSize: 1,
            checksum: $"synthetic-{index + 1:x}",
            uploadedBy: manifest.GetUserId(index % manifest.Definition.UserCount),
            uploadedAtUtc: SeedTimestampUtc.AddSeconds(index))).ToList();
        await SaveInBatchesAsync(context.DocumentAttachments, attachments, context, cancellationToken);

        for (int index = 0; index < documents.Count; index++)
        {
            EnsureSuccess(documents[index].SetSignedCopy(attachments[index].Id));
            EnsureSuccess(documents[index].Submit());
            EnsureSuccess(documents[index].MarkPosted(
                manifest.GetUserId(index % manifest.Definition.UserCount),
                SeedTimestampUtc.AddSeconds(index)));
        }

        await context.SaveChangesAsync(cancellationToken);

        var movements = new List<StockMovement>(manifest.Definition.OperationalMovementCount);
        for (int index = 0; index < manifest.Definition.OperationalMovementCount; index++)
        {
            movements.Add(StockMovement.Create(
                manifest.GetOperationalMovementId(index),
                warehouses[index % warehouses.Length].WarehouseId,
                manifest.GetMaterialId(index % manifest.Definition.MaterialCount),
                documents[index].Id,
                lines[index].Id,
                MovementType.Receipt,
                quantityDelta: 1m,
                postedBy: manifest.GetUserId(index % manifest.Definition.UserCount),
                postedAtUtc: SeedTimestampUtc.AddSeconds(index)).Value);
        }

        await SaveInBatchesAsync(context.StockMovements, movements, context, cancellationToken);

        var auditLogs = new List<AuditLog>(manifest.Definition.AuditRecordCount);
        for (int index = 0; index < manifest.Definition.AuditRecordCount; index++)
        {
            Guid movementId = manifest.GetOperationalMovementId(index % manifest.Definition.OperationalMovementCount);
            auditLogs.Add(AuditLog.Create(
                manifest.GetAuditRecordId(index),
                DeterministicGuid(manifest, "audit-operation", index),
                requestId: null,
                userId: manifest.GetUserId(index % manifest.Definition.UserCount),
                entityType: "StockMovement",
                entityId: movementId,
                aggregateType: "WarehouseDocument",
                aggregateId: documents[index % documents.Count].Id,
                action: AuditActions.Post,
                commandName: "SyntheticDatasetSeed",
                summary: "{\"dataset\":\"synthetic\"}",
                ipAddress: null,
                createdAtUtc: SeedTimestampUtc.AddSeconds(index)).Value);
        }

        await SaveInBatchesAsync(context.AuditLogs, auditLogs, context, cancellationToken);
    }

    private static async Task SaveInBatchesAsync<TEntity>(
        DbSet<TEntity> set,
        IReadOnlyList<TEntity> entities,
        ApplicationDbContext context,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        for (int offset = 0; offset < entities.Count; offset += BatchSize)
        {
            set.AddRange(entities.Skip(offset).Take(BatchSize));
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static void ValidateOptions(SyntheticDatasetSeedOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Seed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The synthetic dataset seed must be positive.");
        }

        if (string.Equals(options.EnvironmentName, "Production", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Synthetic dataset seeding is forbidden in Production.");
        }

        if (!string.Equals(options.EnvironmentName, "Test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Synthetic dataset seeding requires the explicit Test environment.");
        }

        var connection = new NpgsqlConnectionStringBuilder(options.ConnectionString);
        if (string.IsNullOrWhiteSpace(connection.Database) ||
            !string.Equals(connection.Database, options.DatabaseName, StringComparison.Ordinal) ||
            !connection.Database.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Synthetic dataset seeding requires an explicit connection to a database whose name contains 'test'.");
        }
    }

    private static async Task EnsureRunTableAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS synthetic_dataset_runs (
                profile text PRIMARY KEY,
                manifest_hash character(64) NOT NULL,
                seed bigint NOT NULL,
                created_at_utc timestamp with time zone NOT NULL
            )
            """;
        await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static async Task<string?> GetRunHashAsync(
        ApplicationDbContext context,
        DatasetProfile profile,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT manifest_hash FROM synthetic_dataset_runs WHERE profile = @profile",
            (NpgsqlConnection)context.Database.GetDbConnection(),
            (NpgsqlTransaction?)context.Database.CurrentTransaction?.GetDbTransaction());
        command.Parameters.AddWithValue("profile", profile.ToString());
        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    private static async Task AcquireDatasetLockAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@resource_key, 0))",
            (NpgsqlConnection)context.Database.GetDbConnection(),
            (NpgsqlTransaction?)context.Database.CurrentTransaction?.GetDbTransaction());
        command.Parameters.AddWithValue("resource_key", AdvisoryLockResource);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RegisterRunAsync(
        ApplicationDbContext context,
        SyntheticDatasetManifest manifest,
        string manifestHash,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO synthetic_dataset_runs (profile, manifest_hash, seed, created_at_utc)
            VALUES (@profile, @manifest_hash, @seed, @created_at_utc)
            """,
            (NpgsqlConnection)context.Database.GetDbConnection(),
            (NpgsqlTransaction?)context.Database.CurrentTransaction?.GetDbTransaction());
        command.Parameters.AddWithValue("profile", manifest.Profile.ToString());
        command.Parameters.AddWithValue("manifest_hash", manifestHash);
        command.Parameters.AddWithValue("seed", manifest.Seed);
        command.Parameters.AddWithValue("created_at_utc", SeedTimestampUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<SyntheticDatasetSeedResult> GetResultAsync(
        ApplicationDbContext context,
        SyntheticDatasetManifest manifest,
        bool wasAlreadySeeded,
        CancellationToken cancellationToken)
    {
        Guid[] movementIds = Enumerable.Range(0, manifest.Definition.OperationalMovementCount)
            .Select(manifest.GetOperationalMovementId)
            .ToArray();
        Guid[] auditLogIds = Enumerable.Range(0, manifest.Definition.AuditRecordCount)
            .Select(manifest.GetAuditRecordId)
            .ToArray();

        return new SyntheticDatasetSeedResult(
            manifest.Profile,
            manifest.Seed,
            await context.StockMovements.CountAsync(movement => movementIds.Contains(movement.Id), cancellationToken),
            await context.AuditLogs.CountAsync(log => auditLogIds.Contains(log.Id), cancellationToken),
            wasAlreadySeeded);
    }

    private static Guid DeterministicGuid(SyntheticDatasetManifest manifest, string kind, int index)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{manifest.Profile}:{manifest.Seed}:{kind}:{index}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    internal static string ComputeManifestHash(SyntheticDatasetManifest manifest)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendCanonicalValue(hash, "generator-version", GeneratorVersion);
        AppendCanonicalValue(hash, "dataset-schema-version", DatasetSchemaVersion);
        AppendCanonicalValue(hash, "identity-algorithm-version", IdentityAlgorithmVersion);
        AppendCanonicalValue(hash, "profile", manifest.Profile.ToString());
        AppendCanonicalValue(hash, "seed", manifest.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendCanonicalValue(hash, "seed-timestamp-utc", SeedTimestampUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

        SyntheticDatasetProfileDefinition definition = manifest.Definition;
        AppendCanonicalValue(hash, "operational-movement-count", definition.OperationalMovementCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendCanonicalValue(hash, "audit-record-count", definition.AuditRecordCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendCanonicalValue(hash, "organization-count", definition.OrganizationCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendCanonicalValue(hash, "site-count", definition.SiteCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendCanonicalValue(hash, "organizational-unit-count", definition.OrganizationalUnitCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendCanonicalValue(hash, "warehouse-count", definition.WarehouseCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendCanonicalValue(hash, "user-count", definition.UserCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendCanonicalValue(hash, "material-count", definition.MaterialCount.ToString(System.Globalization.CultureInfo.InvariantCulture));

        for (int index = 0; index < IdentityKindLabels.Length; index++)
        {
            AppendCanonicalValue(hash, $"identity-kind-label[{index}]", IdentityKindLabels[index]);
        }

        for (int index = 0; index < definition.OrganizationCount; index++)
        {
            AppendCanonicalValue(hash, $"organization-id[{index}]", manifest.GetOrganizationId(index).ToString("N"));
        }

        for (int index = 0; index < manifest.Sites.Count; index++)
        {
            SyntheticDatasetSite site = manifest.Sites[index];
            AppendCanonicalValue(hash, $"site-organization-id[{index}]", site.OrganizationId.ToString("N"));
            AppendCanonicalValue(hash, $"site-id[{index}]", site.SiteId.ToString("N"));
            for (int unitIndex = 0; unitIndex < site.OrganizationalUnitIds.Count; unitIndex++)
            {
                AppendCanonicalValue(hash, $"organizational-unit-id[{index},{unitIndex}]", site.OrganizationalUnitIds[unitIndex].ToString("N"));
            }

            for (int warehouseIndex = 0; warehouseIndex < site.Warehouses.Count; warehouseIndex++)
            {
                SyntheticDatasetWarehouse warehouse = site.Warehouses[warehouseIndex];
                AppendCanonicalValue(hash, $"warehouse-id[{index},{warehouseIndex}]", warehouse.WarehouseId.ToString("N"));
                AppendCanonicalValue(hash, $"warehouse-organizational-unit-id[{index},{warehouseIndex}]", warehouse.OrganizationalUnitId.ToString("N"));
            }
        }

        for (int index = 0; index < manifest.RoleIds.Count; index++)
        {
            AppendCanonicalValue(hash, $"role-id[{index}]", manifest.RoleIds[index].ToString("N"));
        }

        for (int index = 0; index < definition.UserCount; index++)
        {
            AppendCanonicalValue(hash, $"user-id[{index}]", manifest.GetUserId(index).ToString("N"));
            SyntheticUserScopeAssignment assignment = manifest.UserScopeAssignments[index];
            AppendCanonicalValue(hash, $"scope-user-id[{index}]", assignment.UserId.ToString("N"));
            AppendCanonicalValue(hash, $"scope-role-id[{index}]", assignment.RoleId.ToString("N"));
            AppendCanonicalValue(hash, $"scope-type[{index}]", assignment.ScopeType.ToString());
            AppendCanonicalValue(hash, $"scope-id[{index}]", assignment.ScopeId?.ToString("N") ?? "<null>");
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void AppendCanonicalValue(IncrementalHash hash, string name, string value)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(name));
        hash.AppendData("="u8);
        hash.AppendData(Encoding.UTF8.GetBytes(value));
        hash.AppendData("\n"u8);
    }

    private static void EnsureSuccess(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException("Synthetic dataset construction violated a domain invariant.");
        }
    }

    private sealed class NoOpDomainEventsDispatcher : IDomainEventsDispatcher
    {
        public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

internal sealed record SyntheticDatasetSeedOptions(
    string ConnectionString,
    string DatabaseName,
    string EnvironmentName,
    long Seed);

internal sealed record SyntheticDatasetSeedResult(
    DatasetProfile Profile,
    long Seed,
    int OperationalMovementCount,
    int AuditRecordCount,
    bool WasAlreadySeeded);
