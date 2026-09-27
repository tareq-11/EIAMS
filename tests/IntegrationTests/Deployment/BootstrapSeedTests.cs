using System.Net.Http.Json;
using Application.Abstractions.Authorization;
using Domain.Common;
using Domain.Permissions;
using Domain.Roles;
using Domain.UserRoleScopes;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Deployment;

[Collection(nameof(IntegrationTestCollection))]
public sealed class BootstrapSeedTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public BootstrapSeedTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task SeedData_Should_ContainWellKnownRolesAndPermissions()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Runtime permissions are the dotted-only v1 catalog; legacy identifiers are retained
        // only as migration/audit history and must not appear in the active permission catalog.
        List<Permission> permissions = await context.Permissions.ToListAsync();
        permissions.Select(permission => permission.Code).ToHashSet(StringComparer.Ordinal)
            .SetEquals(PermissionVocabulary.DottedV1Codes).ShouldBeTrue();
        permissions.ShouldContain(permission => permission.Id == WellKnownDottedPermissions.DocumentCreateId);
        permissions.ShouldContain(permission => permission.Id == WellKnownDottedPermissions.DocumentPostId);
        permissions.ShouldContain(permission => permission.Id == WellKnownDottedPermissions.AuditViewId);

        // Assert Well-known roles exist
        List<Role> roles = await context.Roles.ToListAsync();
        roles.ShouldContain(r => r.Id == WellKnownRoles.AdministratorId);
        roles.ShouldContain(r => r.Id == WellKnownRoles.WarehouseKeeperId);
        roles.ShouldContain(r => r.Id == WellKnownRoles.WarehouseManagerId);
    }
}
