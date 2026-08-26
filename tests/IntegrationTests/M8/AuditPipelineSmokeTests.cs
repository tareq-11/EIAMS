using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Audit;
using Domain.Common;
using Domain.Permissions;
using Domain.Roles;
using Domain.UserRoleScopes;
using Infrastructure.Database;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.M8;

[Collection(nameof(IntegrationTestCollection))]
public sealed class AuditPipelineSmokeTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public AuditPipelineSmokeTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public void AccessorResolution_Should_ReturnSameInstanceWithinScope_AndDifferentInstancesAcrossScopes()
    {
        // Arrange
        using IServiceScope firstScope = factory.Services.CreateScope();
        using IServiceScope secondScope = factory.Services.CreateScope();

        // Act
        IAuditOperationContextAccessor first =
            firstScope.ServiceProvider.GetRequiredService<IAuditOperationContextAccessor>();
        IAuditOperationContextAccessor firstAgain =
            firstScope.ServiceProvider.GetRequiredService<IAuditOperationContextAccessor>();
        IAuditOperationContextAccessor second =
            secondScope.ServiceProvider.GetRequiredService<IAuditOperationContextAccessor>();

        // Assert
        first.ShouldBeSameAs(firstAgain);
        second.ShouldNotBeSameAs(first);
    }

    [Fact]
    public async Task AuthenticatedPost_Should_CompleteSuccessfully_WhenAuditDecoratorWrapsCommandPipeline()
    {
        // Arrange
        (Guid userId, AccessTokens tokens) = await RegisterAndLoginAsync();
        await GrantOrganizationsManageAsync(userId);
        Authenticate(tokens.AccessToken);
        string code = $"AUD-{Guid.NewGuid():N}";

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "organizations",
            new { name = "Audit pipeline smoke organization", code });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ApiEnvelope<ResourceId>? body = await response.Content.ReadFromJsonAsync<ApiEnvelope<ResourceId>>();
        body.ShouldNotBeNull();
        body.Success.ShouldBeTrue();
    }

    private sealed record ResourceId(Guid Id);

    private async Task GrantOrganizationsManageAsync(Guid userId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var roleId = Guid.NewGuid();
        context.Roles.Add(Role.Create(roleId, $"Role-{roleId:N}", null));
        context.RolePermissions.Add(RolePermission.Create(roleId, WellKnownPermissions.OrganizationsManageId));
        context.UserRoleScopes.Add(UserRoleScope.Create(Guid.NewGuid(), userId, roleId, ScopeType.Enterprise, null));
        await context.SaveChangesAsync();
    }
}
