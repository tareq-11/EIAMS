using System.Net;
using System.Net.Http.Json;
using System.Text;
using Application.Abstractions.Audit;
using Application.Abstractions.Data;
using Domain.AuditLogs;
using Domain.Common;
using Domain.Organizations;
using Domain.Sites;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.M8;

[Collection(nameof(IntegrationTestCollection))]
public sealed class AuditCaptureTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public AuditCaptureTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task UpdateCapture_Should_WriteSingleHeaderWithOldNewEntry_WhenScopeMetadataMatches()
    {
        (Guid userId, AccessTokens _) = await RegisterAndLoginAsync();
        Guid organizationId = await SeedOrganizationAsync("Original audit organization");

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = GetContext(scope);
        IAuditOperationContextAccessor accessor = GetAccessor(scope);

        Organization organization =
            await context.Organizations.SingleAsync(o => o.Id == organizationId);
        organization.UpdateDetails("Renamed audit organization");

        AuditOperationDescriptor descriptor =
            NewDescriptor("ProbeUpdateCommand", "req-update-capture", userId);

        using (IDisposable _ = accessor.BeginScope(descriptor))
        {
            await context.SaveChangesAsync();
        }

        // Assert
        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext verifyContext = GetContext(verifyScope);

        List<AuditLog> headers = await verifyContext.AuditLogs.AsNoTracking()
            .Where(l => l.EntityType == "Organization" && l.EntityId == organizationId)
            .ToListAsync();

        headers.Count(h => h.Action == AuditActions.Update).ShouldBe(1);

        AuditLog header = headers.Single(h => h.Action == AuditActions.Update);
        header.OperationId.ShouldBe(descriptor.OperationId);
        header.RequestId.ShouldBe("req-update-capture");
        header.UserId.ShouldBe(userId);
        header.IpAddress.ShouldBe("203.0.113.7");
        header.CommandName.ShouldBe("ProbeUpdateCommand");
        header.AggregateType.ShouldBeNull();
        header.AggregateId.ShouldBeNull();

        List<AuditLogEntry> entries = await verifyContext.AuditLogEntries.AsNoTracking()
            .Where(e => e.AuditLogId == header.Id)
            .ToListAsync();

        AuditLogEntry nameEntry = entries.Single(e => e.FieldName == "name");
        nameEntry.NewValue.ShouldBe("Renamed audit organization");
        nameEntry.OldValue.ShouldBe("Original audit organization");
        nameEntry.OldValue.ShouldNotBe(nameEntry.NewValue);
    }

    [Fact]
    public async Task CreateAndDeleteCapture_Should_WriteNullToValueAndValueToNullEntries_WhenEntityLifecycleCompletes()
    {
        var organizationId = Guid.NewGuid();
        const string organizationName = "Lifecycle audit organization";

        await using (AsyncServiceScope arrangeScope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = GetContext(arrangeScope);
            IAuditOperationContextAccessor accessor = GetAccessor(arrangeScope);

            using IDisposable _ = accessor.BeginScope(NewDescriptor("ProbeCreateCommand", "req-create"));
            context.Organizations.Add(
                Organization.Create(organizationId, organizationName, $"LIFE-{organizationId:N}"));

            await context.SaveChangesAsync();
        }

        await using (AsyncServiceScope deleteScope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = GetContext(deleteScope);
            IAuditOperationContextAccessor accessor = GetAccessor(deleteScope);

            Organization organization =
                await context.Organizations.SingleAsync(o => o.Id == organizationId);

            using IDisposable _ = accessor.BeginScope(NewDescriptor("ProbeDeleteCommand", "req-delete"));
            context.Organizations.Remove(organization);

            await context.SaveChangesAsync();
        }

        // Assert
        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext verifyContext = GetContext(verifyScope);

        AuditLog createHeader = await verifyContext.AuditLogs.AsNoTracking()
            .SingleAsync(l => l.EntityType == "Organization" &&
                l.EntityId == organizationId &&
                l.Action == AuditActions.Create);

        AuditLogEntry createNameEntry = await verifyContext.AuditLogEntries.AsNoTracking()
            .SingleAsync(e => e.AuditLogId == createHeader.Id && e.FieldName == "name");

        createNameEntry.OldValue.ShouldBeNull();
        createNameEntry.NewValue.ShouldBe(organizationName);

        AuditLog deleteHeader = await verifyContext.AuditLogs.AsNoTracking()
            .SingleAsync(l => l.EntityType == "Organization" &&
                l.EntityId == organizationId &&
                l.Action == AuditActions.Delete);

        AuditLogEntry deleteNameEntry = await verifyContext.AuditLogEntries.AsNoTracking()
            .SingleAsync(e => e.AuditLogId == deleteHeader.Id && e.FieldName == "name");

        deleteNameEntry.OldValue.ShouldBe(organizationName);
        deleteNameEntry.NewValue.ShouldBeNull();
    }

    [Fact]
    public async Task NoOpUpdate_Should_WriteNoHeader_WhenNothingTrulyChanged()
    {
        const string organizationName = "Frozen audit organization";
        Guid organizationId = await SeedOrganizationAsync(organizationName);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = GetContext(scope);
        IAuditOperationContextAccessor accessor = GetAccessor(scope);

        int headersBefore = await context.AuditLogs.AsNoTracking()
            .CountAsync(l => l.EntityType == "Organization" && l.EntityId == organizationId);

        Organization organization =
            await context.Organizations.SingleAsync(o => o.Id == organizationId);
        organization.UpdateDetails(organizationName);

        using IDisposable _ = accessor.BeginScope(NewDescriptor("ProbeNoOpCommand", "req-no-op"));
        await context.SaveChangesAsync();

        // Assert
        int headersAfter = await context.AuditLogs.AsNoTracking()
            .CountAsync(l => l.EntityType == "Organization" && l.EntityId == organizationId);

        headersAfter.ShouldBe(headersBefore);
    }

    [Fact]
    public async Task RollbackAtomicity_Should_PersistNeitherBusinessNorAuditRows_WhenSecondSaveThrows()
    {
        const string originalName = "Pre-rollback audit organization";
        Guid organizationId = await SeedOrganizationAsync(originalName);
        string duplicateCode = $"ROLLBACK-{Guid.NewGuid():N}";
        await SeedOrganizationAsync("Existing duplicate-code organization", duplicateCode);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = GetContext(scope);
        IAuditOperationContextAccessor accessor = GetAccessor(scope);
        IApplicationTransaction transaction =
            scope.ServiceProvider.GetRequiredService<IApplicationTransaction>();

        Organization organization =
            await context.Organizations.SingleAsync(o => o.Id == organizationId);
        organization.UpdateDetails("Renamed before rollback failure");

        using IDisposable _ = accessor.BeginScope(NewDescriptor("ProbeRollbackCommand", "req-rollback"));

        await Should.ThrowAsync<DbUpdateException>(() => transaction.ExecuteAsync<bool>(async cancellationToken =>
        {
            await context.SaveChangesAsync(cancellationToken);

            context.Organizations.Add(
                Organization.Create(Guid.NewGuid(), "Duplicate", duplicateCode));

            await context.SaveChangesAsync(cancellationToken);

            return Result.Success(true);
        }, CancellationToken.None));

        // Assert
        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext verifyContext = GetContext(verifyScope);

        Organization persisted = await verifyContext.Organizations
            .SingleAsync(o => o.Id == organizationId);
        persisted.Name.ShouldBe(originalName);

        int duplicateCount = await verifyContext.Organizations
            .CountAsync(o => o.Code == duplicateCode);
        duplicateCount.ShouldBe(1);

        int updateHeaders = await verifyContext.AuditLogs.AsNoTracking()
            .CountAsync(l => l.EntityType == "Organization" &&
                l.EntityId == organizationId &&
                l.Action == AuditActions.Update);
        updateHeaders.ShouldBe(0);
    }

    [Fact]
    public async Task MultiSaveCorrelation_Should_ShareOperationIdWithinScope_AndUseNewOperationIdInNewScope()
    {
        Guid firstOrganizationId = await SeedOrganizationAsync("Multi-save organization one");
        Guid secondOrganizationId = await SeedOrganizationAsync("Multi-save organization two");

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = GetContext(scope);
        IAuditOperationContextAccessor accessor = GetAccessor(scope);

        Organization firstOrganization =
            await context.Organizations.SingleAsync(o => o.Id == firstOrganizationId);
        Organization secondOrganization =
            await context.Organizations.SingleAsync(o => o.Id == secondOrganizationId);

        var sharedOperationId = Guid.NewGuid();
        using (IDisposable _ = accessor.BeginScope(NewDescriptorWithOperation(
                   sharedOperationId,
                   "ProbeMultiCommand",
                   "req-multi")))
        {
            firstOrganization.UpdateDetails("Multi-save rename one");
            await context.SaveChangesAsync();

            secondOrganization.UpdateDetails("Multi-save rename two");
            await context.SaveChangesAsync();
        }

        var nextOperationId = Guid.NewGuid();
        using (IDisposable _ = accessor.BeginScope(NewDescriptorWithOperation(
                   nextOperationId,
                   "ProbeMultiCommand",
                   "req-multi-later")))
        {
            firstOrganization.UpdateDetails("Multi-save rename three");
            await context.SaveChangesAsync();
        }

        // Assert
        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext verifyContext = GetContext(verifyScope);

        AuditLog firstSaveHeader = await FindUpdateHeaderByNameEntry(
            verifyContext,
            firstOrganizationId,
            "Multi-save rename one");
        AuditLog secondSaveHeader = await FindUpdateHeaderByNameEntry(
            verifyContext,
            secondOrganizationId,
            "Multi-save rename two");
        AuditLog thirdSaveHeader = await FindUpdateHeaderByNameEntry(
            verifyContext,
            firstOrganizationId,
            "Multi-save rename three");

        firstSaveHeader.OperationId.ShouldBe(sharedOperationId);
        secondSaveHeader.OperationId.ShouldBe(sharedOperationId);
        thirdSaveHeader.OperationId.ShouldBe(nextOperationId);
        firstSaveHeader.Id.ShouldNotBe(thirdSaveHeader.Id);
    }

    [Fact]
    public async Task RetrySafety_Should_ProduceExactlyOneHeader_WhenFirstAttemptFailsOnConcurrency()
    {
        Guid warehouseId = await SeedWarehouseChainAsync();
        var documentId = Guid.NewGuid();

        await using (AsyncServiceScope arrangeScope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext arrangeContext = GetContext(arrangeScope);
            arrangeContext.WarehouseDocuments.Add(WarehouseDocument.CreateDraft(
                documentId,
                warehouseId,
                DocumentType.Receiving,
                $"AUD-{Guid.NewGuid():N}"));

            await arrangeContext.SaveChangesAsync();
        }

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = GetContext(scope);
        IAuditOperationContextAccessor accessor = GetAccessor(scope);

        WarehouseDocument document =
            await context.WarehouseDocuments.SingleAsync(d => d.Id == documentId);
        document.UpdatePaperReference("PR-2026-0001", 2026);

        await context.Database.ExecuteSqlRawAsync(
            "UPDATE warehouse_documents SET row_version = row_version + 1 WHERE id = {0}",
            documentId);

        using (IDisposable _ = accessor.BeginScope(NewDescriptor("ProbeRetryCommand", "req-retry")))
        {
            await Should.ThrowAsync<DbUpdateConcurrencyException>(
                () => context.SaveChangesAsync());

            int currentRowVersion = await context.Database.SqlQuery<int>(
                $"""
                 SELECT row_version AS "Value"
                 FROM warehouse_documents
                 WHERE id = {documentId}
                 """).SingleAsync();

            context.Entry(document)
                .Property(nameof(WarehouseDocument.RowVersion))
                .OriginalValue = currentRowVersion;

            await context.SaveChangesAsync();
        }

        // Assert
        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext verifyContext = GetContext(verifyScope);

        int updateHeaders = await verifyContext.AuditLogs.AsNoTracking()
            .CountAsync(l => l.EntityType == "WarehouseDocument" &&
                l.EntityId == documentId &&
                l.Action == AuditActions.Update);

        updateHeaders.ShouldBe(1);

        int paperReferenceEntries = await verifyContext.AuditLogEntries.AsNoTracking()
            .CountAsync(e => e.FieldName == "paper_document_number" &&
                e.NewValue == "PR-2026-0001" &&
                e.OldValue == null);

        paperReferenceEntries.ShouldBe(1);
    }

    [Fact]
    public async Task RecursionGuard_Should_WriteZeroAuditOfAuditRows_WhenCapturesExecute()
    {
        Guid organizationId = await SeedOrganizationAsync("Recursion guard organization");

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = GetContext(scope);
        IAuditOperationContextAccessor accessor = GetAccessor(scope);

        Organization organization =
            await context.Organizations.SingleAsync(o => o.Id == organizationId);
        organization.UpdateDetails("Recursion guard renamed");

        using IDisposable _ = accessor.BeginScope(NewDescriptor("ProbeRecursionCommand", "req-recursion-guard"));
        await context.SaveChangesAsync();

        // Assert
        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext verifyContext = GetContext(verifyScope);

        int leakedHeaders = await verifyContext.Database.SqlQuery<int>(
            $"""
            SELECT COUNT(*)::int AS "Value"
            FROM audit_logs
            WHERE entity_type IN ('AuditLog', 'AuditLogEntry')
            """).SingleAsync();

        int leakedEntries = await verifyContext.Database.SqlQuery<int>(
            $"""
            SELECT COUNT(*)::int AS "Value"
            FROM audit_log_entries e
            WHERE EXISTS (
                SELECT 1 FROM audit_logs h
                WHERE h.id = e.audit_log_id
                  AND h.entity_type IN ('AuditLog', 'AuditLogEntry'))
            """).SingleAsync();

        leakedHeaders.ShouldBe(0);
        leakedEntries.ShouldBe(0);
    }

    [Fact]
    public async Task Secrets_Should_NeverAppearInAuditTables_AndSyntheticAuthHeadersShouldExist()
    {
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();

        HttpResponseMessage refreshResponse = await HttpClient.PostAsJsonAsync(
            "auth/refresh",
            new { refreshToken = tokens.RefreshToken });

        refreshResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Assert
        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext verifyContext = GetContext(verifyScope);

        List<string> secrets = [];

        secrets.AddRange(await verifyContext.Database.SqlQuery<string>(
            $"""SELECT token AS "Value" FROM refresh_tokens WHERE user_id = {userId}""").ToListAsync());

        secrets.AddRange(await verifyContext.Database.SqlQuery<string>(
            $"""SELECT password_hash AS "Value" FROM users WHERE id = {userId}""").ToListAsync());

        secrets.RemoveAll(string.IsNullOrEmpty);
        secrets.ShouldNotBeEmpty();

        List<AuditLog> headers = await verifyContext.AuditLogs.AsNoTracking().ToListAsync();
        List<AuditLogEntry> entries = await verifyContext.AuditLogEntries.AsNoTracking().ToListAsync();

        foreach (string secret in secrets)
        {
            foreach (AuditLog header in headers)
            {
                HeaderText(header).Contains(secret, StringComparison.Ordinal)
                    .ShouldBeFalse($"Header {header.Id} leaked a secret.");
            }

            foreach (AuditLogEntry entry in entries)
            {
                bool oldLeaked = entry.OldValue?.Contains(secret, StringComparison.Ordinal) ?? false;
                bool newLeaked = entry.NewValue?.Contains(secret, StringComparison.Ordinal) ?? false;

                oldLeaked.ShouldBeFalse($"Entry {entry.Id} old value leaked a secret.");
                newLeaked.ShouldBeFalse($"Entry {entry.Id} new value leaked a secret.");
            }
        }

        AuditLog authenticateHeader = headers.Single(h =>
            h.EntityType == "User" &&
            h.EntityId == userId &&
            h.Action == AuditActions.Authenticate);
        authenticateHeader.UserId.ShouldBe(userId);

        headers.Any(h =>
                h.EntityType == "User" &&
                h.EntityId == userId &&
                h.Action == AuditActions.TokenRefresh)
            .ShouldBeTrue();
    }

    private static ApplicationDbContext GetContext(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    private static IAuditOperationContextAccessor GetAccessor(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IAuditOperationContextAccessor>();

    private static AuditOperationDescriptor NewDescriptor(string commandName, string requestId, Guid? userId = null) =>
        NewDescriptorWithOperation(Guid.NewGuid(), commandName, requestId, userId);

    private static AuditOperationDescriptor NewDescriptorWithOperation(
        Guid operationId,
        string commandName,
        string requestId,
        Guid? userId = null) =>
        new(operationId, requestId, userId, "203.0.113.7", commandName, AuditOperationKind.Http);

    private static string HeaderText(AuditLog header) =>
        new StringBuilder()
            .Append(header.RequestId)
            .Append('|')
            .Append(header.CommandName)
            .Append('|')
            .Append(header.IpAddress)
            .Append('|')
            .Append(header.Summary)
            .ToString();

    private static async Task<AuditLog> FindUpdateHeaderByNameEntry(
        ApplicationDbContext context,
        Guid entityId,
        string newNameValue)
    {
        return await context.AuditLogs.AsNoTracking()
            .Where(l => l.EntityType == "Organization" &&
                l.EntityId == entityId &&
                l.Action == AuditActions.Update)
            .Join(
                context.AuditLogEntries.AsNoTracking()
                    .Where(e => e.FieldName == "name" && e.NewValue == newNameValue),
                l => l.Id,
                e => e.AuditLogId,
                (l, _) => l)
            .SingleAsync();
    }

    private async Task<Guid> SeedOrganizationAsync(string name, string? code = null)
    {
        var organizationId = Guid.NewGuid();

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = GetContext(scope);
        context.Organizations.Add(Organization.Create(
            organizationId,
            name,
            code ?? $"SEED-{organizationId:N}"));
        await context.SaveChangesAsync();

        return organizationId;
    }

    private async Task<Guid> SeedWarehouseChainAsync()
    {
        var organizationId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = GetContext(scope);

        context.Organizations.Add(Organization.Create(
            organizationId,
            "Retry safety organization",
            $"RETRY-O-{organizationId:N}"));
        context.Sites.Add(Site.Create(siteId, organizationId, "Retry safety site", $"RETRY-S-{siteId:N}", null));
        context.Warehouses.Add(Warehouse.Create(
            warehouseId,
            siteId,
            "Retry safety warehouse",
            $"RETRY-W-{warehouseId:N}",
            "MAIN",
            true));

        await context.SaveChangesAsync();

        return warehouseId;
    }
}
