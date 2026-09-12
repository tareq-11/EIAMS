using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Abstractions.Authorization;
using Domain.AuditLogs;
using Domain.Common;
using Domain.Permissions;
using Domain.Roles;
using Domain.UserRoleScopes;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.M8;

[Collection(nameof(IntegrationTestCollection))]
public sealed class AuditLogApiTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public AuditLogApiTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
        this.factory = factory;
    }

    [Theory]
    [InlineData("audit-logs/10000000-0000-0000-0000-000000000001")]
    [InlineData("audit-logs/entities/Organization/10000000-0000-0000-0000-000000000001")]
    [InlineData("audit-logs/users/10000000-0000-0000-0000-000000000001")]
    [InlineData("audit-logs/fields/name")]
    public async Task ReadRoutes_Should_ReturnUnauthorized_WhenTokenIsMissing(string route)
    {
        // Arrange

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(route);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReadRoutes_Should_ReturnForbidden_WhenPermissionIsMissing()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("audit-logs/fields/name");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EntityUserAndFieldRoutes_Should_ReturnTheSameAuditedChange_WithPagination()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        var organizationId = Guid.NewGuid();
        Guid auditLogId = await SeedAuditLogAsync(userId, organizationId);
        await GrantAuditViewAsync(userId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage entityResponse = await HttpClient.GetAsync(
            $"audit-logs/entities/Organization/{organizationId}?action=Update&page=1&pageSize=10");
        HttpResponseMessage userResponse = await HttpClient.GetAsync(
            $"audit-logs/users/{userId}?entityType=Organization&page=1&pageSize=10");
        HttpResponseMessage fieldResponse = await HttpClient.GetAsync(
            $"audit-logs/fields/name?entityType=Organization&entityId={organizationId}&page=1&pageSize=10");

        // Assert
        entityResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        userResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        fieldResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AssertPageContainsOnlyAsync(entityResponse, auditLogId);
        await AssertPageContainsOnlyAsync(userResponse, auditLogId);
        await AssertPageContainsOnlyAsync(fieldResponse, auditLogId);
    }

    [Fact]
    public async Task DetailsRoute_Should_ReturnOrderedFieldDiffs_AndUnknownIdShouldReturnNotFound()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        Guid auditLogId = await SeedAuditLogAsync(userId, Guid.NewGuid());
        await GrantAuditViewAsync(userId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync($"audit-logs/{auditLogId}");
        HttpResponseMessage missing = await HttpClient.GetAsync($"audit-logs/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement data = body.RootElement.GetProperty("data");
        data.GetProperty("id").GetGuid().ShouldBe(auditLogId);
        JsonElement.ArrayEnumerator entries = data.GetProperty("entries").EnumerateArray();
        entries.Select(item => item.GetProperty("fieldName").GetString())
            .ShouldBe(["code", "name"]);

        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InvalidFilters_Should_ReturnStandardBadRequest()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantAuditViewAsync(userId);
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(
            $"audit-logs/entities/Unknown/{Guid.NewGuid()}?action=NotAnAction");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("success").GetBoolean().ShouldBeFalse();
        body.RootElement.GetProperty("error").GetProperty("code").GetString()
            .ShouldBe("AUDIT_LOGS_FILTER_INVALID");
    }

    private async Task<Guid> SeedAuditLogAsync(Guid userId, Guid organizationId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var auditLogId = Guid.NewGuid();

        Result<AuditLog> log = AuditLog.Create(
            auditLogId,
            Guid.NewGuid(),
            $"req-{Guid.NewGuid():N}",
            userId,
            "Organization",
            organizationId,
            null,
            null,
            AuditActions.Update,
            "AuditLogApiProbeCommand",
            null,
            "203.0.113.10",
            DateTime.UtcNow);
        log.IsSuccess.ShouldBeTrue();

        Result<AuditLogEntry> nameEntry = AuditLogEntry.Create(
            Guid.NewGuid(), auditLogId, "name", "Before", "After");
        Result<AuditLogEntry> codeEntry = AuditLogEntry.Create(
            Guid.NewGuid(), auditLogId, "code", "OLD", "NEW");
        nameEntry.IsSuccess.ShouldBeTrue();
        codeEntry.IsSuccess.ShouldBeTrue();

        context.AuditLogs.Add(log.Value);
        context.AuditLogEntries.AddRange(nameEntry.Value, codeEntry.Value);
        await context.SaveChangesAsync();

        return auditLogId;
    }

    private async Task GrantAuditViewAsync(Guid userId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (await context.UserRoleScopes.AnyAsync(assignment => assignment.UserId == userId))
        {
            return;
        }

        var roleId = Guid.NewGuid();

        context.Roles.Add(Role.Create(roleId, $"AuditViewer-{roleId:N}", null));
        context.RolePermissions.Add(RolePermission.Create(roleId, WellKnownPermissions.AuditLogsViewId));
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(), userId, roleId, ScopeType.Enterprise, null));
        await context.SaveChangesAsync();
    }

    private static async Task AssertPageContainsOnlyAsync(HttpResponseMessage response, Guid auditLogId)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = body.RootElement;
        root.GetProperty("success").GetBoolean().ShouldBeTrue();
        root.GetProperty("data").GetProperty("page_info").GetProperty("total_items").GetInt32().ShouldBe(1);
        JsonElement.ArrayEnumerator data = root.GetProperty("data").GetProperty("items").EnumerateArray();
        data.Select(item => item.GetProperty("id").GetGuid()).ShouldBe([auditLogId]);
    }
}
