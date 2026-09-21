using System.Net;
using System.Text.Json;
using Domain.Common;
using Domain.Permissions;
using Domain.Roles;
using Domain.UserRoleScopes;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Performance;

[Collection(nameof(IntegrationTestCollection))]
public sealed class AuditKeysetPaginationTests : BaseIntegrationTest, IDisposable
{
    private readonly IntegrationTestWebAppFactory factory;

    public AuditKeysetPaginationTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task CursorEndpoint_ShouldReturnStableNonOverlappingPagesWithoutCountQuery()
    {
        await AuthenticateAsAdministratorAsync();
        await GrantAuditReaderAsync();

        SqlCommandCounterInterceptor commandCounter = factory.Services
            .GetRequiredService<SqlCommandCounterInterceptor>();
        commandCounter.Reset();

        HttpResponseMessage firstResponse = await HttpClient.GetAsync("audit-logs/cursor?pageSize=1");
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var first = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        JsonElement firstItem = first.RootElement.GetProperty("data")[0];
        Guid firstId = firstItem.GetProperty("id").GetGuid();
        JsonElement pagination = first.RootElement.GetProperty("pagination");
        pagination.GetProperty("has_next_page").GetBoolean().ShouldBeTrue();
        DateTime nextCreatedAtUtc = pagination.GetProperty("next_created_at_utc").GetDateTime();
        Guid nextId = pagination.GetProperty("next_id").GetGuid();

        commandCounter.GetCommandTexts().Any(command =>
            command.Contains("COUNT(", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();

        string secondPageUrl = $"audit-logs/cursor?pageSize=1&afterCreatedAtUtc={nextCreatedAtUtc:O}&afterId={nextId}";
        HttpResponseMessage secondResponse = await HttpClient.GetAsync(secondPageUrl);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var second = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
        Guid secondId = second.RootElement.GetProperty("data")[0].GetProperty("id").GetGuid();

        secondId.ShouldNotBe(firstId);
    }

    [Fact]
    public async Task CursorEndpoint_ShouldRejectIncompleteCursor()
    {
        await AuthenticateAsAdministratorAsync();
        await GrantAuditReaderAsync();

        HttpResponseMessage response = await HttpClient.GetAsync(
            $"audit-logs/cursor?pageSize=20&afterId={Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task GrantAuditReaderAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Guid userId = await context.Users.Where(user => user.Email == IntegrationTestWebAppFactory.AdministratorEmail)
            .Select(user => user.Id).SingleAsync();
        await context.UserRoleScopes.Where(assignment => assignment.UserId == userId).ExecuteDeleteAsync();
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(),
            userId,
            WellKnownRoles.AuditorId,
            ScopeType.Enterprise,
            null));
        await context.SaveChangesAsync();
    }

    public void Dispose()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Guid userId = context.Users.Where(user => user.Email == IntegrationTestWebAppFactory.AdministratorEmail)
            .Select(user => user.Id).Single();
        context.UserRoleScopes.RemoveRange(context.UserRoleScopes.Where(assignment => assignment.UserId == userId));
        context.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(), userId, WellKnownRoles.AdministratorId, ScopeType.Enterprise, null));
        context.SaveChanges();
    }
}
