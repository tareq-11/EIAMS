using Application.Abstractions.Authorization;
using Domain.Common;
using Domain.Organizations;
using Domain.OrganizationalUnits;
using Domain.Permissions;
using Domain.Roles;
using Domain.Sites;
using Domain.UserRoleScopes;
using Domain.Users;
using Domain.Warehouses;
using Infrastructure.Database;
using IntegrationTests.Performance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Authorization;

[Collection(nameof(IntegrationTestCollection))]
public sealed class HierarchicalScopeQueryIntegrationTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public HierarchicalScopeQueryIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task OrganizationalUnitScope_ExpandsOnlyDescendants_UsingBoundedQueriesAndOneArrayParameter()
    {
        HierarchySeed seed = await SeedHierarchyAsync(depth: 2, includeOutsideSibling: true);
        SqlCommandCounterInterceptor commandCounter = factory.Services.GetRequiredService<SqlCommandCounterInterceptor>();

        await using AsyncServiceScope authorizationScope = factory.Services.CreateAsyncScope();
        IScopeAuthorizationService authorization = authorizationScope.ServiceProvider
            .GetRequiredService<IScopeAuthorizationService>();

        commandCounter.Reset();
        WarehousePermissionScope scope = await authorization.GetWarehousePermissionScopeAsync(
            seed.UserId,
            PermissionCodes.Warehouses.View,
            CancellationToken.None);

        scope.HasEnterpriseAccess.ShouldBeFalse();
        scope.WarehouseIds.ShouldContain(seed.ScopedWarehouseIds[0]);
        scope.WarehouseIds.ShouldContain(seed.ScopedWarehouseIds[1]);
        scope.WarehouseIds.ShouldContain(seed.ScopedWarehouseIds[2]);
        scope.WarehouseIds.ShouldNotContain(seed.OutsideWarehouseId!.Value);

        // Cold authorization is version + grants + recursive descendants + warehouses: no per-level query.
        commandCounter.CommandCount.ShouldBeLessThanOrEqualTo(4);
        IReadOnlyList<string> commands = commandCounter.GetCommandTexts();
        commands.Count(command => command.Contains("WITH RECURSIVE", StringComparison.OrdinalIgnoreCase)).ShouldBe(1);
        commands.Any(command => command.Contains("= ANY", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(ScopeType.Enterprise)]
    [InlineData(ScopeType.Site)]
    [InlineData(ScopeType.OrganizationalUnit)]
    [InlineData(ScopeType.Warehouse)]
    public async Task WarehousePermissionScope_ExpandsEachSingleAssignmentWithoutCrossScopeLeakage(ScopeType scopeType)
    {
        HierarchySeed seed = await SeedHierarchyAsync(depth: 2, includeOutsideSibling: true, scopeType);

        await using AsyncServiceScope authorizationScope = factory.Services.CreateAsyncScope();
        IScopeAuthorizationService authorization = authorizationScope.ServiceProvider
            .GetRequiredService<IScopeAuthorizationService>();

        WarehousePermissionScope scope = await authorization.GetWarehousePermissionScopeAsync(
            seed.UserId,
            PermissionCodes.Warehouses.View,
            CancellationToken.None);

        if (scopeType == ScopeType.Enterprise)
        {
            scope.HasEnterpriseAccess.ShouldBeTrue();
            scope.WarehouseIds.ShouldBeEmpty();
            return;
        }

        scope.HasEnterpriseAccess.ShouldBeFalse();
        if (scopeType == ScopeType.Warehouse)
        {
            scope.WarehouseIds.ShouldBe([seed.ScopedWarehouseIds[0]]);
            return;
        }

        scope.WarehouseIds.ShouldContain(seed.ScopedWarehouseIds[0]);
        scope.WarehouseIds.ShouldContain(seed.ScopedWarehouseIds[1]);
        scope.WarehouseIds.ShouldContain(seed.ScopedWarehouseIds[2]);
        if (scopeType == ScopeType.OrganizationalUnit)
        {
            scope.WarehouseIds.ShouldNotContain(seed.OutsideWarehouseId!.Value);
        }
        else
        {
            scope.WarehouseIds.ShouldContain(seed.OutsideWarehouseId!.Value);
        }
    }

    [Fact]
    public async Task OrganizationalUnitScope_CycleInImportedData_IsTerminatedWithoutCrossScopeExpansion()
    {
        HierarchySeed seed = await SeedHierarchyAsync(depth: 1, includeOutsideSibling: true);

        await using (AsyncServiceScope mutationScope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = mutationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE public.organizational_units
                SET parent_id = {seed.ScopedUnitIds[1]}
                WHERE id = {seed.ScopedUnitIds[0]}
                """);
        }

        await using AsyncServiceScope authorizationScope = factory.Services.CreateAsyncScope();
        IScopeAuthorizationService authorization = authorizationScope.ServiceProvider
            .GetRequiredService<IScopeAuthorizationService>();
        using CancellationTokenSource timeout = new();
        timeout.CancelAfter(TimeSpan.FromSeconds(2));

        OrganizationalUnitPermissionScope scope = await authorization.GetOrganizationalUnitPermissionScopeAsync(
            seed.UserId,
            PermissionCodes.OrganizationalUnits.View,
            timeout.Token);

        scope.HasEnterpriseAccess.ShouldBeFalse();
        scope.OrganizationalUnitIds.Count.ShouldBe(2);
        scope.OrganizationalUnitIds.ShouldContain(seed.ScopedUnitIds[0]);
        scope.OrganizationalUnitIds.ShouldContain(seed.ScopedUnitIds[1]);
        scope.OrganizationalUnitIds.ShouldNotContain(seed.OutsideUnitId!.Value);
    }

    [Fact]
    public async Task OrganizationalUnitScope_StopsAtConfiguredMaximumDepth()
    {
        const int depthPastRoot = 65;
        HierarchySeed seed = await SeedHierarchyAsync(depthPastRoot, includeOutsideSibling: false);

        await using AsyncServiceScope authorizationScope = factory.Services.CreateAsyncScope();
        IScopeAuthorizationService authorization = authorizationScope.ServiceProvider
            .GetRequiredService<IScopeAuthorizationService>();

        OrganizationalUnitPermissionScope scope = await authorization.GetOrganizationalUnitPermissionScopeAsync(
            seed.UserId,
            PermissionCodes.OrganizationalUnits.View,
            CancellationToken.None);

        scope.OrganizationalUnitIds.Count.ShouldBe(65); // root plus 64 descendants.
        scope.OrganizationalUnitIds.ShouldContain(seed.ScopedUnitIds[64]);
        scope.OrganizationalUnitIds.ShouldNotContain(seed.ScopedUnitIds[65]);
    }

    [Fact]
    public async Task OrganizationalUnitScope_PropagatesAnAlreadyCancelledTokenToDatabaseWork()
    {
        await using AsyncServiceScope authorizationScope = factory.Services.CreateAsyncScope();
        IScopeAuthorizationService authorization = authorizationScope.ServiceProvider
            .GetRequiredService<IScopeAuthorizationService>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => authorization.GetWarehousePermissionScopeAsync(
            Guid.NewGuid(),
            PermissionCodes.Warehouses.View,
            cancelled.Token));
    }

    private async Task<HierarchySeed> SeedHierarchyAsync(
        int depth,
        bool includeOutsideSibling,
        ScopeType scopeType = ScopeType.OrganizationalUnit)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var suffix = Guid.NewGuid();
        var organization = Organization.Create(Guid.NewGuid(), $"Scope organization {suffix:N}", $"ORG-{suffix:N}"[..16]);
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Scope site {suffix:N}", $"SITE-{suffix:N}"[..16], "Test");
        context.AddRange(organization, site);

        var scopedUnits = new List<OrganizationalUnit>(depth + 1);
        Guid? parentId = null;
        for (int index = 0; index <= depth; index++)
        {
            var unit = OrganizationalUnit.Create(
                Guid.NewGuid(), site.Id, parentId, $"Scoped unit {suffix:N}-{index}", "Department");
            scopedUnits.Add(unit);
            parentId = unit.Id;
        }

        OrganizationalUnit? outsideUnit = includeOutsideSibling
            ? OrganizationalUnit.Create(Guid.NewGuid(), site.Id, null, $"Outside unit {suffix:N}", "Department")
            : null;
        context.OrganizationalUnits.AddRange(scopedUnits);
        if (outsideUnit is not null)
        {
            context.OrganizationalUnits.Add(outsideUnit);
        }

        var scopedWarehouses = scopedUnits.Select((unit, index) => Warehouse.Create(
            Guid.NewGuid(), site.Id, $"Scoped warehouse {suffix:N}-{index}", $"S{index}-{suffix:N}"[..20], "Test", true, unit.Id)).ToList();
        context.Warehouses.AddRange(scopedWarehouses);
        Warehouse? outsideWarehouse = outsideUnit is null
            ? null
            : Warehouse.Create(Guid.NewGuid(), site.Id, $"Outside warehouse {suffix:N}", $"OUT-{suffix:N}"[..20], "Test", true, outsideUnit.Id);
        if (outsideWarehouse is not null)
        {
            context.Warehouses.Add(outsideWarehouse);
        }

        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var user = User.Create(userId, $"scope-{suffix:N}@test.com", "Scope", "User", "hash");
        var role = Role.Create(roleId, $"Scope role {suffix:N}", "Scope query integration role");
        Guid? scopeId = scopeType switch
        {
            ScopeType.Enterprise => null,
            ScopeType.Site => site.Id,
            ScopeType.OrganizationalUnit => scopedUnits[0].Id,
            ScopeType.Warehouse => scopedWarehouses[0].Id,
            _ => throw new ArgumentOutOfRangeException(nameof(scopeType), scopeType, null)
        };
        context.AddRange(
            user,
            role,
            RolePermission.Create(roleId, WellKnownPermissions.WarehousesViewId),
            RolePermission.Create(roleId, WellKnownPermissions.OrganizationalUnitsViewId),
            UserRoleScope.Create(Guid.NewGuid(), userId, roleId, scopeType, scopeId));
        await context.SaveChangesAsync(CancellationToken.None);

        return new HierarchySeed(
            userId,
            scopedUnits.Select(unit => unit.Id).ToArray(),
            scopedWarehouses.Select(warehouse => warehouse.Id).ToArray(),
            outsideUnit?.Id,
            outsideWarehouse?.Id);
    }

    private sealed record HierarchySeed(
        Guid UserId,
        Guid[] ScopedUnitIds,
        Guid[] ScopedWarehouseIds,
        Guid? OutsideUnitId,
        Guid? OutsideWarehouseId);
}
