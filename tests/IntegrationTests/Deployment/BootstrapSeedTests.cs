using System.Net.Http.Json;
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

        // Assert Well-known permissions exist
        List<Permission> permissions = await context.Permissions.ToListAsync();
        permissions.Count.ShouldBeGreaterThan(0);
        permissions.ShouldContain(p => p.Id == WellKnownPermissions.WarehouseDocumentsCreateId);
        permissions.ShouldContain(p => p.Id == WellKnownPermissions.WarehouseDocumentsReviewId);
        permissions.ShouldContain(p => p.Id == WellKnownPermissions.AuditLogsViewId);

        // Assert Well-known roles exist
        List<Role> roles = await context.Roles.ToListAsync();
        roles.ShouldContain(r => r.Id == WellKnownRoles.AdministratorId);
        roles.ShouldContain(r => r.Id == WellKnownRoles.WarehouseKeeperId);
        roles.ShouldContain(r => r.Id == WellKnownRoles.WarehouseManagerId);
    }
}
