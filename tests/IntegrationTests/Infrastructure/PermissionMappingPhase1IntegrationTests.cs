using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Users.GetSession;
using Domain.Common;
using Domain.OrganizationalUnits;
using Domain.Organizations;
using Domain.Permissions;
using Domain.Roles;
using Domain.Sites;
using Domain.UserRoleScopes;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SharedKernel;

namespace IntegrationTests.Infrastructure;

[Collection(nameof(IntegrationTestCollection))]
public sealed class PermissionMappingPhase1IntegrationTests(IntegrationTestWebAppFactory factory)
{
    [Theory]
    [InlineData(ScopeType.Enterprise)]
    [InlineData(ScopeType.Site)]
    public async Task EffectivePermissions_ShouldDenyOperationalMutationOutsideWarehouse_WhenAssignmentAndRoleScopeAreValid(
        ScopeType scopeType)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction isolation =
            await context.Database.BeginTransactionAsync();
        IEffectivePermissionService effective = scope.ServiceProvider.GetRequiredService<IEffectivePermissionService>();

        Guid? scopeId = null;
        if (scopeType is ScopeType.Site)
        {
            var organization = Organization.Create(Guid.NewGuid(), $"Boundary org {Guid.NewGuid():N}", $"B{Guid.NewGuid():N}"[..12]);
            var site = Site.Create(Guid.NewGuid(), organization.Id, "Boundary site", $"S{Guid.NewGuid():N}"[..12], null);
            context.Organizations.Add(organization);
            context.Sites.Add(site);
            scopeId = site.Id;

        }

        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        context.AddRange(
            User.Create(userId, $"boundary-{userId:N}@example.test", "Boundary", "Scope", "hash"),
            Role.Create(roleId, $"Valid {scopeType} boundary role", null),
            RoleAllowedScopeType.Create(roleId, scopeType),
            RolePermission.Create(roleId, WellKnownDottedPermissions.DocumentCreateId),
            UserRoleScope.Create(Guid.NewGuid(), userId, roleId, scopeType, scopeId));
        await context.SaveChangesAsync();

        IReadOnlyList<string> codes = await effective.GetEffectivePermissionCodesAsync(userId, CancellationToken.None);
        codes.ShouldNotContain("document.create");
        await isolation.RollbackAsync();
    }

    [Fact]
    public async Task SessionPermissions_ShouldMatchEffectiveService_AndExcludeDormantDottedCodes()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction isolation =
            await context.Database.BeginTransactionAsync();
        IEffectivePermissionService effective = scope.ServiceProvider.GetRequiredService<IEffectivePermissionService>();
        IQueryHandler<GetUserSessionQuery, UserSessionResponse> sessionHandler =
            scope.ServiceProvider.GetRequiredService<IQueryHandler<GetUserSessionQuery, UserSessionResponse>>();

        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var dormantPermissionId = Guid.NewGuid();
        string dormantCode = $"document.revise.{userId:N}";
        context.AddRange(
            User.Create(userId, $"session-{userId:N}@example.test", "Session", "Parity", "hash"),
            Role.Create(roleId, "Session parity role", null),
            RoleAllowedScopeType.Create(roleId, ScopeType.Warehouse),
            RolePermission.Create(roleId, WellKnownDottedPermissions.DocumentCreateId),
            Permission.Create(dormantPermissionId, dormantCode, "Dormant dotted permission"),
            RolePermission.Create(roleId, dormantPermissionId),
            UserRoleScope.Create(Guid.NewGuid(), userId, roleId, ScopeType.Warehouse, Guid.NewGuid()));
        await context.SaveChangesAsync();

        IReadOnlyList<string> expected = await effective.GetEffectivePermissionCodesAsync(userId, CancellationToken.None);
        Result<UserSessionResponse> sessionResult = await sessionHandler.Handle(new GetUserSessionQuery(userId), CancellationToken.None);

        sessionResult.IsSuccess.ShouldBeTrue();
        sessionResult.Value.PermissionCodes.ShouldBe(expected);
        sessionResult.Value.PermissionCodes.ShouldContain("document.create");
        sessionResult.Value.PermissionCodes.ShouldNotContain(dormantCode);
        await isolation.RollbackAsync();
    }

    [Fact]
    public async Task EffectivePermissions_ShouldRequireRoleScopeAndActiveVocabulary()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction isolation =
            await context.Database.BeginTransactionAsync();
        IEffectivePermissionService effective = scope.ServiceProvider.GetRequiredService<IEffectivePermissionService>();

        var invalidUserId = Guid.NewGuid();
        var invalidRoleId = Guid.NewGuid();
        context.AddRange(
            User.Create(invalidUserId, $"invalid-{invalidUserId:N}@example.test", "Invalid", "Scope", "hash"),
            Role.Create(invalidRoleId, "Invalid scope role", null),
            RolePermission.Create(invalidRoleId, WellKnownDottedPermissions.DocumentCreateId),
            UserRoleScope.Create(Guid.NewGuid(), invalidUserId, invalidRoleId, ScopeType.Warehouse, Guid.NewGuid()));
        await context.SaveChangesAsync();

        (await effective.GetEffectivePermissionCodesAsync(invalidUserId, CancellationToken.None))
            .ShouldNotContain("document.create");

        var validUserId = Guid.NewGuid();
        var validRoleId = Guid.NewGuid();
        var dormantPermissionId = Guid.NewGuid();
        string dormantCode = $"document.revise.{validUserId:N}";
        context.AddRange(
            User.Create(validUserId, $"valid-{validUserId:N}@example.test", "Valid", "Scope", "hash"),
            Role.Create(validRoleId, "Valid warehouse role", null),
            RoleAllowedScopeType.Create(validRoleId, ScopeType.Warehouse),
            RolePermission.Create(validRoleId, WellKnownDottedPermissions.DocumentCreateId),
            Permission.Create(dormantPermissionId, dormantCode, "Dormant dotted permission"),
            RolePermission.Create(validRoleId, dormantPermissionId),
            UserRoleScope.Create(Guid.NewGuid(), validUserId, validRoleId, ScopeType.Warehouse, Guid.NewGuid()));
        await context.SaveChangesAsync();

        IReadOnlyList<string> codes = await effective.GetEffectivePermissionCodesAsync(validUserId, CancellationToken.None);
        codes.ShouldContain("document.create");
        codes.ShouldNotContain(dormantCode);
        await isolation.RollbackAsync();
    }

    [Fact]
    public async Task PermissionScopeMatrix_ShouldContainNoLegacyRowsAfterDottedCutover()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        (await context.Permissions.AsNoTracking().CountAsync(permission => EF.Functions.Like(permission.Code, "%:%"))).ShouldBe(0);

        string[] codes = await context.Permissions.AsNoTracking().Select(permission => permission.Code).ToArrayAsync();
        codes.ToHashSet(StringComparer.Ordinal).SetEquals(PermissionVocabulary.DottedV1Codes).ShouldBeTrue();

        ScopeType[] allowedAssignmentScopes = [ScopeType.Enterprise, ScopeType.Site, ScopeType.Warehouse];
        (await context.PermissionAllowedScopeTypes.AsNoTracking().Select(item => item.ScopeType).Distinct().ToListAsync())
            .ToHashSet().IsSubsetOf(allowedAssignmentScopes).ShouldBeTrue();
        (await context.RoleAllowedScopeTypes.AsNoTracking().Select(item => item.ScopeType).Distinct().ToListAsync())
            .ToHashSet().IsSubsetOf(allowedAssignmentScopes).ShouldBeTrue();
    }

    [Fact]
    public async Task DottedCatalog_ShouldContainExactCodesWithNewStableIds()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Dictionary<string, Guid> expected = new(StringComparer.Ordinal)
        {
            ["asset.view"] = WellKnownDottedPermissions.AssetViewId,
            ["audit.view"] = WellKnownDottedPermissions.AuditViewId,
            ["custody.assign"] = WellKnownDottedPermissions.CustodyAssignId,
            ["organization.view"] = WellKnownDottedPermissions.OrganizationViewId,
            ["organization.manage"] = WellKnownDottedPermissions.OrganizationManageId,
            ["admin.role.view"] = WellKnownDottedPermissions.AdminRoleViewId,
            ["admin.role.manage"] = WellKnownDottedPermissions.AdminRoleManageId,
            ["catalog.manage"] = WellKnownDottedPermissions.CatalogManageId,
            ["catalog.view"] = WellKnownDottedPermissions.CatalogViewId,
            ["warehouse.manage"] = WellKnownDottedPermissions.WarehouseManageId,
            ["warehouse.view"] = WellKnownDottedPermissions.WarehouseViewId,
            ["inventory.view"] = WellKnownDottedPermissions.InventoryViewId,
            ["document.view"] = WellKnownDottedPermissions.DocumentViewId,
            ["document.create"] = WellKnownDottedPermissions.DocumentCreateId,
            ["document.update"] = WellKnownDottedPermissions.DocumentUpdateId,
            ["document.submit"] = WellKnownDottedPermissions.DocumentSubmitId,
            ["document.post"] = WellKnownDottedPermissions.DocumentPostId,
            ["document.reject"] = WellKnownDottedPermissions.DocumentRejectId,
            ["document.cancel"] = WellKnownDottedPermissions.DocumentCancelId,
            ["document.reverse"] = WellKnownDottedPermissions.DocumentReverseId,
            ["count.view"] = WellKnownDottedPermissions.CountViewId,
            ["count.plan"] = WellKnownDottedPermissions.CountPlanId,
            ["count.enter"] = WellKnownDottedPermissions.CountEnterId,
            ["count.complete"] = WellKnownDottedPermissions.CountCompleteId,
            ["count.close"] = WellKnownDottedPermissions.CountCloseId,
            ["admin.user.view"] = WellKnownDottedPermissions.AdminUserViewId,
            ["admin.user.manage"] = WellKnownDottedPermissions.AdminUserManageId,
            ["document.revise"] = WellKnownDottedPermissions.DocumentReviseId,
            ["report.view"] = WellKnownDottedPermissions.ReportViewId
        };

        Dictionary<string, Guid> actual = await context.Permissions
            .AsNoTracking()
            .Where(permission => PermissionVocabulary.DottedV1Codes.Contains(permission.Code))
            .ToDictionaryAsync(permission => permission.Code, permission => permission.Id);

        actual.ShouldBe(expected);
        actual.Values.Intersect(PermissionVocabulary.LegacyColonCodes
                .Select(code => context.Permissions.Where(permission => permission.Code == code)
                    .Select(permission => permission.Id).FirstOrDefault()))
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task DottedRoleSeeds_ShouldMatchApprovedRoleGrantSetsAndScopeAssignments()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var expectedGrants = new Dictionary<Guid, HashSet<Guid>>
        {
            [WellKnownRoles.AdministratorId] =
            [
                WellKnownDottedPermissions.CatalogViewId, WellKnownDottedPermissions.CatalogManageId,
                WellKnownDottedPermissions.OrganizationViewId, WellKnownDottedPermissions.OrganizationManageId,
                WellKnownDottedPermissions.WarehouseViewId, WellKnownDottedPermissions.WarehouseManageId,
                WellKnownDottedPermissions.AdminUserViewId, WellKnownDottedPermissions.AdminUserManageId,
                WellKnownDottedPermissions.AdminRoleViewId, WellKnownDottedPermissions.AdminRoleManageId
            ],
            [WellKnownRoles.WarehouseManagerId] =
            [
                WellKnownDottedPermissions.CatalogViewId, WellKnownDottedPermissions.OrganizationViewId,
                WellKnownDottedPermissions.WarehouseViewId, WellKnownDottedPermissions.InventoryViewId,
                WellKnownDottedPermissions.DocumentViewId, WellKnownDottedPermissions.CountViewId,
                WellKnownDottedPermissions.AssetViewId, WellKnownDottedPermissions.ReportViewId,
                WellKnownDottedPermissions.DocumentCreateId, WellKnownDottedPermissions.DocumentUpdateId,
                WellKnownDottedPermissions.DocumentPostId, WellKnownDottedPermissions.DocumentRejectId,
                WellKnownDottedPermissions.DocumentCancelId, WellKnownDottedPermissions.DocumentReverseId,
                WellKnownDottedPermissions.CountPlanId, WellKnownDottedPermissions.CountCompleteId,
                WellKnownDottedPermissions.CountCloseId
            ],
            [WellKnownRoles.WarehouseKeeperId] =
            [
                WellKnownDottedPermissions.CatalogViewId, WellKnownDottedPermissions.OrganizationViewId,
                WellKnownDottedPermissions.WarehouseViewId, WellKnownDottedPermissions.InventoryViewId,
                WellKnownDottedPermissions.DocumentViewId, WellKnownDottedPermissions.DocumentCreateId,
                WellKnownDottedPermissions.DocumentUpdateId, WellKnownDottedPermissions.DocumentSubmitId,
                WellKnownDottedPermissions.DocumentReviseId, WellKnownDottedPermissions.DocumentCancelId,
                WellKnownDottedPermissions.CountViewId, WellKnownDottedPermissions.CountEnterId,
                WellKnownDottedPermissions.AssetViewId, WellKnownDottedPermissions.CustodyAssignId,
                WellKnownDottedPermissions.ReportViewId
            ],
            [WellKnownRoles.AuditorId] =
            [
                WellKnownDottedPermissions.CatalogViewId, WellKnownDottedPermissions.OrganizationViewId,
                WellKnownDottedPermissions.WarehouseViewId, WellKnownDottedPermissions.InventoryViewId,
                WellKnownDottedPermissions.DocumentViewId, WellKnownDottedPermissions.CountViewId,
                WellKnownDottedPermissions.AssetViewId, WellKnownDottedPermissions.AuditViewId,
                WellKnownDottedPermissions.ReportViewId
            ]
        };

        foreach ((Guid roleId, HashSet<Guid> expected) in expectedGrants)
        {
            var actual = (await context.RolePermissions
                    .AsNoTracking()
                    .Where(grant => grant.RoleId == roleId && PermissionVocabulary.DottedV1Codes.Contains(
                        context.Permissions.Where(permission => permission.Id == grant.PermissionId)
                            .Select(permission => permission.Code).First()))
                    .Select(grant => grant.PermissionId)
                    .ToListAsync())
                .ToHashSet();
            actual.SetEquals(expected).ShouldBeTrue();
        }

        Dictionary<Guid, HashSet<ScopeType>> scopes = await context.RoleAllowedScopeTypes
            .AsNoTracking()
            .Where(item => expectedGrants.Keys.Contains(item.RoleId))
            .GroupBy(item => item.RoleId)
            .ToDictionaryAsync(group => group.Key, group => group.Select(item => item.ScopeType).ToHashSet());
        scopes[WellKnownRoles.AdministratorId].SetEquals([ScopeType.Enterprise]).ShouldBeTrue();
        scopes[WellKnownRoles.WarehouseKeeperId].SetEquals([ScopeType.Warehouse]).ShouldBeTrue();
        scopes[WellKnownRoles.WarehouseManagerId].SetEquals([ScopeType.Enterprise, ScopeType.Site, ScopeType.Warehouse]).ShouldBeTrue();
        scopes[WellKnownRoles.AuditorId].SetEquals([ScopeType.Enterprise, ScopeType.Site, ScopeType.Warehouse]).ShouldBeTrue();
        (await context.Roles.AsNoTracking().SingleAsync(role => role.Id == WellKnownRoles.AdministratorId)).Name.ShouldBe("SYSTEM_ADMIN");
    }

    [Fact]
    public async Task DottedPermissionScopeMatrix_ShouldContainExactApprovedPairs()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Guid[] reads =
        [
            WellKnownDottedPermissions.AssetViewId, WellKnownDottedPermissions.AuditViewId,
            WellKnownDottedPermissions.OrganizationViewId, WellKnownDottedPermissions.CatalogViewId,
            WellKnownDottedPermissions.WarehouseViewId, WellKnownDottedPermissions.InventoryViewId,
            WellKnownDottedPermissions.DocumentViewId, WellKnownDottedPermissions.CountViewId,
            WellKnownDottedPermissions.ReportViewId
        ];
        Guid[] structural =
        [
            WellKnownDottedPermissions.OrganizationManageId, WellKnownDottedPermissions.CatalogManageId,
            WellKnownDottedPermissions.WarehouseManageId, WellKnownDottedPermissions.AdminUserViewId,
            WellKnownDottedPermissions.AdminUserManageId, WellKnownDottedPermissions.AdminRoleViewId,
            WellKnownDottedPermissions.AdminRoleManageId
        ];
        Guid[] operational =
        [
            WellKnownDottedPermissions.CustodyAssignId, WellKnownDottedPermissions.DocumentCreateId,
            WellKnownDottedPermissions.DocumentUpdateId, WellKnownDottedPermissions.DocumentSubmitId,
            WellKnownDottedPermissions.DocumentPostId, WellKnownDottedPermissions.DocumentRejectId,
            WellKnownDottedPermissions.DocumentCancelId, WellKnownDottedPermissions.DocumentReverseId,
            WellKnownDottedPermissions.DocumentReviseId,
            WellKnownDottedPermissions.CountPlanId, WellKnownDottedPermissions.CountEnterId,
            WellKnownDottedPermissions.CountCompleteId, WellKnownDottedPermissions.CountCloseId
        ];
        HashSet<(Guid PermissionId, ScopeType ScopeType)> expected = reads
            .SelectMany(id => new[] { ScopeType.Enterprise, ScopeType.Site, ScopeType.Warehouse }.Select(scopeType => (id, scopeType)))
            .Concat(structural.Select(id => (id, ScopeType.Enterprise)))
            .Concat(operational.Select(id => (id, ScopeType.Warehouse)))
            .ToHashSet();
        var actual = (await context.PermissionAllowedScopeTypes
                .AsNoTracking()
                .Where(item => reads.Contains(item.PermissionId) || structural.Contains(item.PermissionId) || operational.Contains(item.PermissionId))
                .Select(item => new { item.PermissionId, item.ScopeType })
                .ToListAsync())
            .Select(item => (item.PermissionId, item.ScopeType))
            .ToHashSet();
        actual.SetEquals(expected).ShouldBeTrue();
        actual.Count.ShouldBe(expected.Count);
    }

    [Fact]
    public async Task DottedGrants_ShouldBeSelectedAfterDottedCutover()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IEffectivePermissionService effective = scope.ServiceProvider.GetRequiredService<IEffectivePermissionService>();
        var userId = Guid.NewGuid();
        context.AddRange(
            User.Create(userId, $"dotted-dormant-{userId:N}@example.test", "Dormant", "Dotted", "hash"),
            UserRoleScope.Create(Guid.NewGuid(), userId, WellKnownRoles.AdministratorId, ScopeType.Enterprise, null));
        await context.SaveChangesAsync();

        IReadOnlyList<string> codes = await effective.GetEffectivePermissionCodesAsync(userId, CancellationToken.None);
        codes.Intersect(PermissionVocabulary.LegacyColonCodes, StringComparer.Ordinal).ShouldBeEmpty();
        codes.ShouldContain(PermissionCodes.Users.Access);
    }

    [Fact]
    public async Task DottedMarker_ShouldExposeReviseOnlyToWarehouseAssignments_AndRestoreLegacyState()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IEffectivePermissionService effective = scope.ServiceProvider.GetRequiredService<IEffectivePermissionService>();
        var roleId = Guid.NewGuid();
        context.Add(Role.Create(roleId, $"Dotted revise boundary {roleId:N}", null));
        context.Add(RolePermission.Create(roleId, WellKnownDottedPermissions.DocumentReviseId));
        ScopeType[] assignmentScopes = [ScopeType.Enterprise, ScopeType.Site, ScopeType.Warehouse];
        foreach (ScopeType scopeType in assignmentScopes)
        {
            context.Add(RoleAllowedScopeType.Create(roleId, scopeType));
        }

        Dictionary<ScopeType, Guid> users = assignmentScopes.ToDictionary(scopeType => scopeType, _ => Guid.NewGuid());
        foreach ((ScopeType scopeType, Guid userId) in users)
        {
            context.Add(User.Create(userId, $"dotted-revise-{scopeType}-{userId:N}@example.test", "Dotted", scopeType.ToString(), "hash"));
            context.Add(UserRoleScope.Create(Guid.NewGuid(), userId, roleId, scopeType, scopeType == ScopeType.Enterprise ? null : Guid.NewGuid()));
        }
        await context.SaveChangesAsync();

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE authorization_policy_versions SET active_vocabulary = {"dotted-v1"} WHERE is_active = TRUE");

        foreach ((ScopeType scopeType, Guid userId) in users)
        {
            IReadOnlyList<string> codes = await effective.GetEffectivePermissionCodesAsync(userId, CancellationToken.None);
            if (scopeType == ScopeType.Warehouse)
            {
                codes.ShouldContain("document.revise");
            }
            else
            {
                codes.ShouldNotContain("document.revise");
            }
        }

        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task MappingSeed_ShouldContainExpectedPairAndPolicyCounts()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        List<PermissionCodeMapping> mappings = await context.PermissionCodeMappings
            .AsNoTracking()
            .Where(mapping => mapping.MappingVersion == 1)
            .ToListAsync();

        mappings.Count.ShouldBe(42);
        mappings.Select(mapping => mapping.OldCode).Distinct(StringComparer.Ordinal).Count().ShouldBe(38);
        mappings.Select(mapping => mapping.NewCode).Distinct(StringComparer.Ordinal).Count().ShouldBe(27);
        HashSet<(string OldCode, string NewCode, int Version)> expected =
        [
            ("assets:view", "asset.view", 1), ("audit-logs:view", "audit.view", 1),
            ("custody:view", "asset.view", 1), ("custody:manage", "custody.assign", 1),
            ("organizations:view", "organization.view", 1), ("sites:view", "organization.view", 1),
            ("org-units:view", "organization.view", 1), ("employees:view", "organization.view", 1),
            ("organizations:manage", "organization.manage", 1), ("sites:manage", "organization.manage", 1),
            ("org-units:manage", "organization.manage", 1), ("employees:manage", "organization.manage", 1),
            ("roles:view", "admin.role.view", 1), ("roles:manage", "admin.role.manage", 1),
            ("material-categories:manage", "catalog.manage", 1), ("material-domains:manage", "catalog.manage", 1),
            ("material-families:manage", "catalog.manage", 1), ("materials:manage", "catalog.manage", 1),
            ("units-of-measure:manage", "catalog.manage", 1), ("materials:view", "catalog.view", 1),
            ("units-of-measure:view", "catalog.view", 1), ("warehouse-capabilities:manage", "warehouse.manage", 1),
            ("warehouse-material-settings:manage", "warehouse.manage", 1), ("warehouses:manage", "warehouse.manage", 1),
            ("warehouses:view", "warehouse.view", 1), ("inventory:view", "inventory.view", 1),
            ("warehouse-documents:view", "document.view", 1), ("warehouse-documents:create", "document.create", 1),
            ("warehouse-documents:edit", "document.update", 1), ("warehouse-documents:submit", "document.submit", 1),
            ("warehouse-documents:cancel", "document.cancel", 1), ("warehouse-documents:review", "document.post", 1),
            ("warehouse-documents:review", "document.reject", 1), ("warehouse-documents:reverse", "document.reverse", 1),
            ("inventory-counts:view", "count.view", 1), ("inventory-counts:plan", "count.plan", 1),
            ("inventory-counts:enter-actual", "count.enter", 1), ("inventory-counts:review", "count.plan", 1),
            ("inventory-counts:review", "count.complete", 1), ("inventory-counts:review", "count.close", 1),
            ("users:access", "admin.user.view", 1), ("users:access", "admin.user.manage", 1)
        ];
        HashSet<(string OldCode, string NewCode, int Version)> actual = mappings
            .Select(mapping => (mapping.OldCode, mapping.NewCode, mapping.MappingVersion))
            .ToHashSet();
        actual.SetEquals(expected).ShouldBeTrue();
        actual.Count.ShouldBe(expected.Count);
        actual.Count(pair => pair.OldCode == "warehouse-documents:review").ShouldBe(2);
        actual.Count(pair => pair.OldCode == "inventory-counts:review").ShouldBe(3);
        actual.Count(pair => pair.NewCode == "organization.view").ShouldBe(4);
        actual.Count(pair => pair.NewCode == "catalog.manage").ShouldBe(5);

        AuthorizationPolicyVersion marker = await context.AuthorizationPolicyVersions
            .SingleAsync(version => version.IsActive);
        marker.ActiveVocabulary.ShouldBe("dotted-v1");
        marker.MappingVersion.ShouldBe(1);
    }

    [Fact]
    public async Task MappingHistory_ShouldRejectUpdateAndDelete()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        PermissionCodeMapping mapping = await context.PermissionCodeMappings
            .AsNoTracking()
            .FirstAsync();

        PostgresException updateException = await Should.ThrowAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE permission_code_mappings SET rationale = {mapping.Rationale} WHERE id = {mapping.Id}"));
        updateException.SqlState.ShouldBe("55000");
        updateException.MessageText.ShouldBe("permission_code_mappings is append-only");

        PostgresException deleteException = await Should.ThrowAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM permission_code_mappings WHERE id = {mapping.Id}"));
        deleteException.SqlState.ShouldBe("55000");
        deleteException.MessageText.ShouldBe("permission_code_mappings is append-only");
        (await context.PermissionCodeMappings.AnyAsync(item => item.Id == mapping.Id)).ShouldBeTrue();
    }
}
