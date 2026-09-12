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
using Domain.Permissions;
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
/// Explicit, test-only runner for synthetic benchmark datasets. It deliberately accepts
/// a connection string only from its caller so it cannot be reached by normal application paths.
/// </summary>
internal static class SyntheticDatasetSeeder
{
    internal const int DefaultBatchSize = 250;
    private const int MaximumBatchSize = 1_000;
    private const string GeneratorVersion = "synthetic-dataset-generator-v3";
    private const string DatasetSchemaVersion = "synthetic-dataset-schema-v2";
    private const string IdentityAlgorithmVersion = "sha256-guid-v1";
    private const string AdvisoryLockResource = "integration-tests:synthetic-dataset-seeder";
    private static readonly Guid[] SyntheticReadPermissionIds =
    [
        WellKnownPermissions.WarehousesViewId,
        WellKnownPermissions.MaterialsViewId,
        WellKnownPermissions.InventoryViewId,
        WellKnownPermissions.WarehouseDocumentsViewId,
        WellKnownPermissions.AuditLogsViewId
    ];
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

    internal static async Task<SyntheticDatasetSeedResult> SeedAsync(
        DatasetProfile profile,
        SyntheticDatasetSeedOptions options,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(profile, options);

        SyntheticDatasetManifest manifest = SyntheticDatasetManifestFactory.Create(profile, options.Seed);
        string manifestHash = ComputeManifestHash(manifest);
        Guid runId = CreateRunId(manifestHash);

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
            SyntheticDatasetRunMarker? existingRun = await GetRunAsync(context, manifest.Profile, cancellationToken);
            if (existingRun is not null)
            {
                EnsureExistingRunCanBeReused(existingRun, manifestHash, manifest.Profile);

                await transaction.CommitAsync(cancellationToken);
                context.DiscardPostCommitActions();
                return await GetResultAsync(
                    context,
                    manifest,
                    existingRun.RunId!.Value,
                    wasAlreadySeeded: true,
                    cancellationToken);
            }

            await SeedManifestAsync(context, manifest, runId, options.BatchSize, cancellationToken);
            await RegisterRunAsync(context, manifest, manifestHash, runId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            context.DiscardPostCommitActions();
            throw;
        }

        context.DiscardPostCommitActions();
        return await GetResultAsync(context, manifest, runId, wasAlreadySeeded: false, cancellationToken);
    }

    private static async Task SeedManifestAsync(
        ApplicationDbContext context,
        SyntheticDatasetManifest manifest,
        Guid runId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        string prefix = GetDatasetPrefix(manifest);
        Guid unitId = DeterministicGuid(manifest, "unit", 0);
        Guid materialDomainId = DeterministicGuid(manifest, "material-domain", 0);
        Guid materialCategoryId = DeterministicGuid(manifest, "material-category", 0);
        Guid materialFamilyId = DeterministicGuid(manifest, "material-family", 0);

        await SeedReferenceDataAsync(
            context,
            manifest,
            prefix,
            unitId,
            materialDomainId,
            materialCategoryId,
            materialFamilyId,
            batchSize,
            cancellationToken);

        SyntheticDatasetWarehouse[] warehouses = manifest.Sites.SelectMany(site => site.Warehouses).ToArray();
        foreach (SyntheticDatasetBatch batch in CreateBatchPlan(manifest.Definition.OperationalMovementCount, batchSize))
        {
            await SeedOperationalBatchAsync(context, manifest, prefix, unitId, warehouses, batch, cancellationToken);
        }

        foreach (SyntheticDatasetBatch batch in CreateBatchPlan(manifest.Definition.AuditRecordCount, batchSize))
        {
            await SeedAuditBatchAsync(context, manifest, runId, batch, cancellationToken);
        }
    }

    private static async Task SeedReferenceDataAsync(
        ApplicationDbContext context,
        SyntheticDatasetManifest manifest,
        string prefix,
        Guid unitId,
        Guid materialDomainId,
        Guid materialCategoryId,
        Guid materialFamilyId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        await SaveBoundedAsync(
            context.Organizations,
            Enumerable.Range(0, manifest.Definition.OrganizationCount).Select(index =>
                Organization.Create(manifest.GetOrganizationId(index), $"Synthetic organization {index + 1}", $"{prefix}-org-{index + 1}")),
            context,
            batchSize,
            cancellationToken);
        await SaveBoundedAsync(
            context.Sites,
            manifest.Sites.Select((site, index) =>
                Site.Create(site.SiteId, site.OrganizationId, $"Synthetic site {index + 1}", $"{prefix}-site-{index + 1}", "Synthetic test location")),
            context,
            batchSize,
            cancellationToken);
        await SaveBoundedAsync(
            context.OrganizationalUnits,
            manifest.Sites.SelectMany((site, siteIndex) => site.OrganizationalUnitIds.Select((unit, unitIndex) =>
                OrganizationalUnit.Create(unit, site.SiteId, null, $"Synthetic unit {siteIndex + 1}-{unitIndex + 1}", "Benchmark"))),
            context,
            batchSize,
            cancellationToken);
        await SaveBoundedAsync(
            context.Warehouses,
            manifest.Sites.SelectMany((site, siteIndex) => site.Warehouses.Select((warehouse, warehouseIndex) =>
                Warehouse.Create(warehouse.WarehouseId, site.SiteId, $"Synthetic warehouse {siteIndex + 1}-{warehouseIndex + 1}", $"{prefix}-wh-{siteIndex + 1}-{warehouseIndex + 1}", "Benchmark", true, warehouse.OrganizationalUnitId))),
            context,
            batchSize,
            cancellationToken);
        await SaveBoundedAsync(
            context.Roles,
            manifest.RoleIds.Select((roleId, index) =>
                Role.Create(roleId, $"SYN_{prefix}_{index + 1}", "Synthetic benchmark role.")),
            context,
            batchSize,
            cancellationToken);
        await SaveBoundedAsync(
            context.RoleAllowedScopeTypes,
            manifest.RoleIds.Select((roleId, index) =>
                RoleAllowedScopeType.Create(roleId, GetSyntheticRoleScopeType(index))),
            context,
            batchSize,
            cancellationToken);
        await SaveBoundedAsync(
            context.RolePermissions,
            manifest.RoleIds.SelectMany(roleId => SyntheticReadPermissionIds.Select(permissionId =>
                RolePermission.Create(roleId, permissionId))),
            context,
            batchSize,
            cancellationToken);
        await SaveBoundedAsync(
            context.Users,
            Enumerable.Range(0, manifest.Definition.UserCount).Select(index =>
                User.Create(manifest.GetUserId(index), $"{prefix}-user-{index + 1}@synthetic.test", "Synthetic", $"User{index + 1}", "synthetic-dataset-not-for-login")),
            context,
            batchSize,
            cancellationToken);
        await SaveBoundedAsync(
            context.UserRoleScopes,
            manifest.UserScopeAssignments.Select((assignment, index) => UserRoleScope.Create(
                DeterministicGuid(manifest, "user-role-scope", index),
                assignment.UserId,
                assignment.RoleId,
                assignment.ScopeType,
                assignment.ScopeId)),
            context,
            batchSize,
            cancellationToken);
        await SaveBoundedAsync(context.UnitsOfMeasure, [UnitOfMeasure.Create(unitId, "Synthetic unit", "syn", "Quantity")], context, batchSize, cancellationToken);
        await SaveBoundedAsync(context.MaterialDomains, [MaterialDomain.Create(materialDomainId, "Synthetic domain", $"{prefix}-domain")], context, batchSize, cancellationToken);
        await SaveBoundedAsync(context.MaterialCategories, [MaterialCategory.Create(materialCategoryId, materialDomainId, null, "Synthetic category", $"{prefix}-category")], context, batchSize, cancellationToken);
        await SaveBoundedAsync(context.MaterialFamilies, [MaterialFamily.Create(materialFamilyId, materialCategoryId, "Synthetic family", $"{prefix}-family", unitId)], context, batchSize, cancellationToken);
        await SaveBoundedAsync(
            context.Materials,
            Enumerable.Range(0, manifest.Definition.MaterialCount).Select(index => Material.Create(
                manifest.GetMaterialId(index), materialFamilyId, unitId, $"Synthetic material {index + 1}",
                $"Synthetic material {index + 1}", $"{prefix}-mat-{index + 1}", MaterialKind.Consumable,
                TrackingType.Quantity, false, "{\"dataset\":\"synthetic\"}")),
            context,
            batchSize,
            cancellationToken);
    }

    private static async Task SeedOperationalBatchAsync(
        ApplicationDbContext context, SyntheticDatasetManifest manifest, string prefix, Guid unitId,
        SyntheticDatasetWarehouse[] warehouses, SyntheticDatasetBatch batch, CancellationToken cancellationToken)
    {
        // This is intentionally the only per-operational-batch materialization. It is capped by
        // MaximumBatchSize and lets lifecycle transitions use the same domain instances without
        // a database IN query or a movement-sized collection of document ids.
        var documents = new List<WarehouseDocument>(batch.Count);
        for (int index = batch.StartIndex; index < batch.EndExclusive; index++)
        {
            documents.Add(CreateDocument(manifest, prefix, warehouses, index));
        }

        await SaveAndClearAsync(context.WarehouseDocuments, documents, context, cancellationToken);
        await SaveBoundedAsync(context.DocumentLines, EnumerateBatch(batch).Select(index => CreateDocumentLine(manifest, unitId, index)), context, batch.Count, cancellationToken);
        await SaveBoundedAsync(context.DocumentAttachments, EnumerateBatch(batch).Select(index => CreateAttachment(manifest, index)), context, batch.Count, cancellationToken);

        context.WarehouseDocuments.AttachRange(documents);
        for (int offset = 0; offset < documents.Count; offset++)
        {
            WarehouseDocument document = documents[offset];
            int index = batch.StartIndex + offset;
            EnsureSuccess(document.SetSignedCopy(DeterministicGuid(manifest, "attachment", index)));
            EnsureSuccess(document.Submit());
            EnsureSuccess(document.MarkPosted(manifest.GetUserId(index % manifest.Definition.UserCount), SeedTimestampUtc.AddSeconds(index)));
        }
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();
        context.DiscardPostCommitActions();

        await SaveBoundedAsync(
            context.StockMovements,
            EnumerateBatch(batch).Select(index => StockMovement.Create(
                manifest.GetOperationalMovementId(index),
                warehouses[index % warehouses.Length].WarehouseId,
                manifest.GetMaterialId(index % manifest.Definition.MaterialCount),
                DeterministicGuid(manifest, "document", index),
                DeterministicGuid(manifest, "document-line", index),
                MovementType.Receipt,
                1m,
                manifest.GetUserId(index % manifest.Definition.UserCount),
                SeedTimestampUtc.AddSeconds(index)).Value),
            context,
            batch.Count,
            cancellationToken);
    }

    private static async Task SeedAuditBatchAsync(
        ApplicationDbContext context,
        SyntheticDatasetManifest manifest,
        Guid runId,
        SyntheticDatasetBatch batch,
        CancellationToken cancellationToken)
    {
        // Audit rows are explicit because this fixture measures a realistic append-only audit workload;
        // it does not bypass the audit trigger or depend on application request interception.
        await SaveBoundedAsync(
            context.AuditLogs,
            EnumerateBatch(batch).Select(index => AuditLog.Create(
                manifest.GetAuditRecordId(index),
                DeterministicGuid(manifest, "audit-operation", index),
                null,
                manifest.GetUserId(index % manifest.Definition.UserCount),
                "StockMovement",
                manifest.GetOperationalMovementId(index % manifest.Definition.OperationalMovementCount),
                "WarehouseDocument",
                DeterministicGuid(manifest, "document", index % manifest.Definition.OperationalMovementCount),
                AuditActions.Post,
                GetAuditCommandName(runId),
                $"{{\"datasetRunId\":\"{runId:N}\"}}",
                null,
                SeedTimestampUtc.AddSeconds(index)).Value),
            context,
            batch.Count,
            cancellationToken);
    }

    private static IEnumerable<int> EnumerateBatch(SyntheticDatasetBatch batch) =>
        Enumerable.Range(batch.StartIndex, batch.Count);

    private static ScopeType GetSyntheticRoleScopeType(int roleIndex)
    {
        ScopeType[] scopeTypes = Enum.GetValues<ScopeType>();
        return scopeTypes[roleIndex % scopeTypes.Length];
    }

    private static WarehouseDocument CreateDocument(SyntheticDatasetManifest manifest, string prefix, SyntheticDatasetWarehouse[] warehouses, int index)
    {
        var document = WarehouseDocument.CreateDraft(DeterministicGuid(manifest, "document", index), warehouses[index % warehouses.Length].WarehouseId, DocumentType.Receiving, $"{prefix}-doc-{index + 1}");
        EnsureSuccess(document.UpdatePaperReference($"SYN-{index + 1}", 2025));
        return document;
    }

    private static DocumentLine CreateDocumentLine(SyntheticDatasetManifest manifest, Guid unitId, int index) =>
        DocumentLine.Create(
            DeterministicGuid(manifest, "document-line", index),
            DeterministicGuid(manifest, "document", index),
            manifest.GetMaterialId(index % manifest.Definition.MaterialCount),
            DocumentLineType.Normal,
            1m,
            unitId,
            1m,
            1m,
            null,
            null).Value;

    private static DocumentAttachment CreateAttachment(SyntheticDatasetManifest manifest, int index) =>
        DocumentAttachment.Create(
            DeterministicGuid(manifest, "attachment", index),
            DeterministicGuid(manifest, "document", index),
            AttachmentType.SignedOriginal,
            $"synthetic/{manifest.Profile.ToString().ToUpperInvariant()}/{manifest.Seed}/{index + 1}.pdf",
            "synthetic.pdf",
            "application/pdf",
            1,
            $"synthetic-{index + 1:x}",
            manifest.GetUserId(index % manifest.Definition.UserCount),
            SeedTimestampUtc.AddSeconds(index));

    private static async Task SaveBoundedAsync<TEntity>(DbSet<TEntity> set, IEnumerable<TEntity> entities, ApplicationDbContext context, int batchSize, CancellationToken cancellationToken) where TEntity : class
    {
        var batch = new List<TEntity>(batchSize);
        foreach (TEntity entity in entities)
        {
            batch.Add(entity);
            if (batch.Count == batchSize)
            {
                await SaveBatchAsync(set, batch, context, cancellationToken);
            }
        }

        if (batch.Count > 0)
        {
            await SaveBatchAsync(set, batch, context, cancellationToken);
        }
    }

    private static async Task SaveBatchAsync<TEntity>(DbSet<TEntity> set, List<TEntity> batch, ApplicationDbContext context, CancellationToken cancellationToken) where TEntity : class
    {
        await SaveAndClearAsync(set, batch, context, cancellationToken);
        batch.Clear();
    }

    private static async Task SaveAndClearAsync<TEntity>(DbSet<TEntity> set, IEnumerable<TEntity> entities, ApplicationDbContext context, CancellationToken cancellationToken) where TEntity : class
    {
        set.AddRange(entities);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();
        // The test-only seeder persists under one transaction and intentionally has no external
        // post-commit side effects; clear deferred domain/cache work so it cannot grow with rows.
        context.DiscardPostCommitActions();
    }

    internal static IEnumerable<SyntheticDatasetBatch> CreateBatchPlan(int itemCount, int batchSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(itemCount);
        ValidateBatchSize(batchSize);

        for (int startIndex = 0; startIndex < itemCount; startIndex += batchSize)
        {
            yield return new SyntheticDatasetBatch(startIndex, Math.Min(batchSize, itemCount - startIndex));
        }
    }

    internal static void ValidateOptions(DatasetProfile profile, SyntheticDatasetSeedOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Enum.IsDefined(profile))
        {
            throw new ArgumentOutOfRangeException(nameof(profile));
        }

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

        if ((connection.Host?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [])
            .Any(host => host.EndsWith(".neon.tech", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Synthetic dataset seeding is forbidden against Neon connections.");
        }

        ValidateBatchSize(options.BatchSize);
    }

    private static void ValidateBatchSize(int batchSize)
    {
        if (batchSize is < 1 or > MaximumBatchSize)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), $"Batch size must be between 1 and {MaximumBatchSize}.");
        }
    }

    internal static async Task EnsureRunTableAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS synthetic_dataset_runs (
                profile text PRIMARY KEY,
                manifest_hash character(64) NOT NULL,
                run_id uuid NULL,
                seed bigint NOT NULL,
                created_at_utc timestamp with time zone NOT NULL
            );

            ALTER TABLE synthetic_dataset_runs
            ADD COLUMN IF NOT EXISTS run_id uuid NULL;
            """;
        await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    internal static async Task<SyntheticDatasetRunMarker?> GetRunAsync(
        ApplicationDbContext context,
        DatasetProfile profile,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT manifest_hash, run_id FROM synthetic_dataset_runs WHERE profile = @profile",
            (NpgsqlConnection)context.Database.GetDbConnection(),
            (NpgsqlTransaction?)context.Database.CurrentTransaction?.GetDbTransaction());
        command.Parameters.AddWithValue("profile", profile.ToString());
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        Guid? runId = await reader.IsDBNullAsync(1, cancellationToken)
            ? null
            : await reader.GetFieldValueAsync<Guid>(1, cancellationToken);
        return new SyntheticDatasetRunMarker(reader.GetString(0), runId);
    }

    internal static void EnsureExistingRunCanBeReused(
        SyntheticDatasetRunMarker existingRun,
        string manifestHash,
        DatasetProfile profile)
    {
        if (!string.Equals(existingRun.ManifestHash, manifestHash, StringComparison.Ordinal) ||
            existingRun.RunId is null)
        {
            throw new InvalidOperationException(
                $"A different or legacy synthetic dataset manifest is already registered for the {profile} profile. " +
                "Use a fresh test database rather than mixing benchmark datasets.");
        }
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
        Guid runId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO synthetic_dataset_runs (profile, manifest_hash, run_id, seed, created_at_utc)
            VALUES (@profile, @manifest_hash, @run_id, @seed, @created_at_utc)
            """,
            (NpgsqlConnection)context.Database.GetDbConnection(),
            (NpgsqlTransaction?)context.Database.CurrentTransaction?.GetDbTransaction());
        command.Parameters.AddWithValue("profile", manifest.Profile.ToString());
        command.Parameters.AddWithValue("manifest_hash", manifestHash);
        command.Parameters.AddWithValue("run_id", runId);
        command.Parameters.AddWithValue("seed", manifest.Seed);
        command.Parameters.AddWithValue("created_at_utc", SeedTimestampUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<SyntheticDatasetSeedResult> GetResultAsync(
        ApplicationDbContext context,
        SyntheticDatasetManifest manifest,
        Guid runId,
        bool wasAlreadySeeded,
        CancellationToken cancellationToken)
    {
        string prefix = GetDatasetPrefix(manifest);

        return new SyntheticDatasetSeedResult(
            manifest.Profile,
            manifest.Seed,
            await (from movement in context.StockMovements
                   join document in context.WarehouseDocuments on movement.DocumentId equals document.Id
                   where document.SystemReferenceNumber.StartsWith($"{prefix}-doc-")
                   select movement.Id).CountAsync(cancellationToken),
            await context.AuditLogs.CountAsync(log => log.CommandName == GetAuditCommandName(runId), cancellationToken),
            wasAlreadySeeded);
    }

    private static string GetDatasetPrefix(SyntheticDatasetManifest manifest) =>
        $"syn-{manifest.Profile.ToString().ToUpperInvariant()}-{manifest.Seed:x}-{manifest.GetOrganizationId(0).ToString("N")[..8]}";

    private static string GetAuditCommandName(Guid runId) => $"SyntheticDatasetSeed:{runId:N}";

    private static Guid CreateRunId(string manifestHash)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"synthetic-dataset-run:{manifestHash}"));
        return new Guid(hash.AsSpan(0, 16));
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
    long Seed,
    int BatchSize = SyntheticDatasetSeeder.DefaultBatchSize);

internal sealed record SyntheticDatasetSeedResult(
    DatasetProfile Profile,
    long Seed,
    int OperationalMovementCount,
    int AuditRecordCount,
    bool WasAlreadySeeded);

internal sealed record SyntheticDatasetBatch(int StartIndex, int Count)
{
    internal int EndExclusive => checked(StartIndex + Count);
}

internal sealed record SyntheticDatasetRunMarker(string ManifestHash, Guid? RunId);
