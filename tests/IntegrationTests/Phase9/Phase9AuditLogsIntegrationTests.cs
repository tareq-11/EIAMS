using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.AuditLogs;
using Domain.AuditLogs;
using Domain.Common;
using Domain.OrganizationalUnits;
using Domain.Organizations;
using Domain.Permissions;
using Domain.Roles;
using Domain.Sites;
using Domain.UserRoleScopes;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using Shouldly;
using Web.Api.Infrastructure;
using Xunit;

namespace IntegrationTests.Phase9;

public sealed class Phase9AuditLogsIntegrationTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public Phase9AuditLogsIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task GetAuditLogs_Should_ReturnUnauthorized_WhenRequestHasNoToken()
    {
        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("audit-logs");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAuditLogs_Should_ReturnPaginatedLogs_WhenUserHasEnterpriseAccess()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);

        var logId = Guid.NewGuid();
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Result<AuditLog> auditLog = AuditLog.Create(
                logId,
                Guid.NewGuid(),
                "REQ-PHASE9-001",
                userId,
                "WarehouseDocument",
                Guid.NewGuid(),
                null,
                null,
                "Post",
                "PostWarehouseDocumentCommand",
                """{"result":"posted"}""",
                "127.0.0.1",
                DateTime.UtcNow);

            auditLog.IsSuccess.ShouldBeTrue();
            db.AuditLogs.Add(auditLog.Value);
            await db.SaveChangesAsync();
        }

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("audit-logs?entityType=WarehouseDocument&action=Post");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ApiResponse<PagedData<AuditLogListItemResponse>>? content =
            await response.Content.ReadFromJsonAsync<ApiResponse<PagedData<AuditLogListItemResponse>>>();

        content.ShouldNotBeNull();
        content.Data.ShouldNotBeNull();
        content.Data.Items.Any(x => x.Id == logId).ShouldBeTrue();

        AuditLogListItemResponse item = content.Data.Items.First(x => x.Id == logId);
        item.ActionDisplayAr.ShouldBe("ترحيل");
        item.ActionDisplayEn.ShouldBe("Post");
        item.EntityTypeDisplayAr.ShouldBe("مستند مستودعي");
        item.EntityTypeDisplayEn.ShouldBe("Warehouse Document");
    }

    [Fact]
    public async Task GetAuditLogById_Should_RedactSensitiveEntries_WhenQueried()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);

        var logId = Guid.NewGuid();
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Result<AuditLog> auditLog = AuditLog.Create(
                logId,
                Guid.NewGuid(),
                "REQ-PHASE9-002",
                userId,
                "User",
                userId,
                null,
                null,
                "Update",
                "UpdateUserCommand",
                """{"profile":{"api_key":"RAW_SUMMARY_SECRET"}}""",
                "127.0.0.1",
                DateTime.UtcNow);

            Result<AuditLogEntry> entrySensitive = AuditLogEntry.Create(
                Guid.NewGuid(),
                logId,
                "password_hash",
                "OLD_RAW_HASH_SECRET",
                "NEW_RAW_HASH_SECRET");

            Result<AuditLogEntry> entryNormal = AuditLogEntry.Create(
                Guid.NewGuid(),
                logId,
                "email",
                "old@example.com",
                "new@example.com");

            auditLog.IsSuccess.ShouldBeTrue();
            entrySensitive.IsSuccess.ShouldBeTrue();
            entryNormal.IsSuccess.ShouldBeTrue();
            db.AuditLogs.Add(auditLog.Value);
            db.AuditLogEntries.AddRange(entrySensitive.Value, entryNormal.Value);
            await db.SaveChangesAsync();
        }

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"audit-logs/{logId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ApiResponse<AuditLogDetailsResponse>? content =
            await response.Content.ReadFromJsonAsync<ApiResponse<AuditLogDetailsResponse>>();

        content.ShouldNotBeNull();
        content.Data.ShouldNotBeNull();
        (await response.Content.ReadAsStringAsync()).ShouldNotContain("RAW_SUMMARY_SECRET");
        content.Data.Summary.ShouldBeNull();
        content.Data.IsSummaryRedacted.ShouldBeTrue();
        content.Data.SummaryRedactionReason.ShouldBe("SENSITIVE_SUMMARY");
        content.Data.Entries.Count.ShouldBe(2);

        AuditLogEntryResponse passwordEntry = content.Data.Entries.First(e => e.FieldName == "password_hash");
        passwordEntry.IsRedacted.ShouldBeTrue();
        passwordEntry.RedactionReason.ShouldBe("CONFIDENTIAL_CREDENTIAL");
        passwordEntry.OldValue.ShouldBeNull();
        passwordEntry.NewValue.ShouldBeNull();

        AuditLogEntryResponse emailEntry = content.Data.Entries.First(e => e.FieldName == "email");
        emailEntry.IsRedacted.ShouldBeFalse();
        emailEntry.RedactionReason.ShouldBeNull();
        emailEntry.OldValue.ShouldBe("old@example.com");
        emailEntry.NewValue.ShouldBe("new@example.com");
        emailEntry.FieldDisplayAr.ShouldBe("البريد الإلكتروني");
        emailEntry.FieldDisplayEn.ShouldBe("Email");
    }

    [Fact]
    public async Task AuditRoutes_Should_FilterBeforePaginationAndHideOutsideWarehouseScope()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        (Guid allowedWarehouseId, Guid allowedDocumentId, Guid outsideDocumentId) =
            await SeedWarehouseDocumentsAsync();
        (Guid allowedLogId, Guid outsideLogId) = await SeedDocumentAuditLogsAsync(
            userId,
            allowedDocumentId,
            outsideDocumentId);
        await GrantWarehouseAuditViewAsync(userId, allowedWarehouseId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage globalResponse = await HttpClient.GetAsync(
            "audit-logs?entityType=WarehouseDocument&action=Post&page=1&pageSize=1");
        HttpResponseMessage allowedDetailsResponse = await HttpClient.GetAsync($"audit-logs/{allowedLogId}");
        HttpResponseMessage outsideDetailsResponse = await HttpClient.GetAsync($"audit-logs/{outsideLogId}");
        HttpResponseMessage outsideEntityResponse = await HttpClient.GetAsync(
            $"audit-logs/entities/WarehouseDocument/{outsideDocumentId}?page=1&pageSize=10");

        // Assert
        globalResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (var globalBody = JsonDocument.Parse(await globalResponse.Content.ReadAsStringAsync()))
        {
            JsonElement root = globalBody.RootElement;
            root.GetProperty("data").GetProperty("page_info").GetProperty("total_items").GetInt32().ShouldBe(1);
            root.GetProperty("data").GetProperty("items")[0].GetProperty("id").GetGuid().ShouldBe(allowedLogId);
        }

        allowedDetailsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        outsideDetailsResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        outsideEntityResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var outsideEntityBody = JsonDocument.Parse(await outsideEntityResponse.Content.ReadAsStringAsync());
        outsideEntityBody.RootElement.GetProperty("data").GetProperty("page_info").GetProperty("total_items").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task GetAuditLogs_Should_ReturnBadRequest_WhenGlobalFiltersAreInvalid()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            "audit-logs?entityType=Unknown&page=0&pageSize=9999");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SpecializedAuditRoute_Should_ReturnBadRequest_WhenPaginationIsInvalid()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantEnterpriseAdministratorAsync(userId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            $"audit-logs/entities/User/{userId}?page=0&pageSize=9999");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task GrantEnterpriseAdministratorAsync(Guid userId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        bool hasScope = await context.UserRoleScopes.AnyAsync(urs => urs.UserId == userId);
        if (!hasScope)
        {
            var roleScope = UserRoleScope.Create(
                Guid.NewGuid(),
                userId,
                WellKnownRoles.AdministratorId,
                ScopeType.Enterprise,
                null);

            context.UserRoleScopes.Add(roleScope);
            await context.SaveChangesAsync();
        }
    }

    private async Task<(Guid AllowedWarehouseId, Guid AllowedDocumentId, Guid OutsideDocumentId)>
        SeedWarehouseDocumentsAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..12];
        var organization = Organization.Create(Guid.NewGuid(), $"Organization {suffix}", $"ORG{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Site {suffix}", $"S{suffix}", null);
        var organizationalUnit = OrganizationalUnit.Create(
            Guid.NewGuid(), site.Id, null, $"Directorate {suffix}", "Directorate");
        var allowedWarehouse = Warehouse.Create(
            Guid.NewGuid(), site.Id, $"Allowed {suffix}", $"A{suffix}", "General", true, organizationalUnit.Id);
        var outsideWarehouse = Warehouse.Create(
            Guid.NewGuid(), site.Id, $"Outside {suffix}", $"O{suffix}", "General", true, organizationalUnit.Id);
        var allowedDocument = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), allowedWarehouse.Id, DocumentType.Receiving, $"ALLOW-{suffix}");
        var outsideDocument = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), outsideWarehouse.Id, DocumentType.Receiving, $"OUT-{suffix}");

        context.AddRange(
            organization,
            site,
            organizationalUnit,
            allowedWarehouse,
            outsideWarehouse,
            allowedDocument,
            outsideDocument);
        await context.SaveChangesAsync();

        return (allowedWarehouse.Id, allowedDocument.Id, outsideDocument.Id);
    }

    private async Task<(Guid AllowedLogId, Guid OutsideLogId)> SeedDocumentAuditLogsAsync(
        Guid userId,
        Guid allowedDocumentId,
        Guid outsideDocumentId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Result<AuditLog> allowedLog = CreateDocumentAuditLog(userId, allowedDocumentId);
        Result<AuditLog> outsideLog = CreateDocumentAuditLog(userId, outsideDocumentId);
        allowedLog.IsSuccess.ShouldBeTrue();
        outsideLog.IsSuccess.ShouldBeTrue();
        context.AuditLogs.AddRange(allowedLog.Value, outsideLog.Value);
        await context.SaveChangesAsync();
        return (allowedLog.Value.Id, outsideLog.Value.Id);
    }

    private async Task GrantWarehouseAuditViewAsync(Guid userId, Guid warehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleId = Guid.NewGuid();
        context.Roles.Add(Role.Create(roleId, $"WarehouseAuditViewer-{roleId:N}", null));
        context.RolePermissions.Add(RolePermission.Create(roleId, WellKnownPermissions.AuditLogsViewId));

        UserRoleScope? assignment = await context.UserRoleScopes.SingleOrDefaultAsync(item => item.UserId == userId);
        if (assignment is null)
        {
            context.UserRoleScopes.Add(UserRoleScope.Create(
                Guid.NewGuid(), userId, roleId, ScopeType.Warehouse, warehouseId));
        }
        else
        {
            assignment.ReplaceAssignment(roleId, ScopeType.Warehouse, warehouseId);
        }

        await context.SaveChangesAsync();
    }

    private static Result<AuditLog> CreateDocumentAuditLog(Guid userId, Guid documentId) =>
        AuditLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            $"REQ-{Guid.NewGuid():N}",
            userId,
            "WarehouseDocument",
            documentId,
            null,
            null,
            AuditActions.Post,
            "PostDocumentCommand",
            null,
            "127.0.0.1",
            DateTime.UtcNow);
}
