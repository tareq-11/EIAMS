using System.Diagnostics.Metrics;
using Application.Abstractions.Authorization;
using Domain.Common;
using Domain.Permissions;
using Domain.Roles;
using Domain.UserRoleScopes;
using Domain.Users;
using Infrastructure.Database;
using IntegrationTests.Performance;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Authorization;

[Collection(nameof(IntegrationTestCollection))]
public sealed class AuthorizationCacheIntegrationTests
{
    private const string AuthorizationMeterName = "CleanArchitecture.Infrastructure.Authorization";
    private readonly IntegrationTestWebAppFactory factory;

    public AuthorizationCacheIntegrationTests(IntegrationTestWebAppFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task PermissionCheck_ShouldUseOneAuthorizationQueryWhenColdAndNoneWhenWarm()
    {
        // Arrange
        Guid userId = await CreateUserWithPermissionAsync(PermissionCodes.Organizations.Manage);
        HybridCache cache = factory.Services.GetRequiredService<HybridCache>();
        SqlCommandCounterInterceptor commandCounter =
            factory.Services.GetRequiredService<SqlCommandCounterInterceptor>();
        await cache.RemoveByTagAsync("auth-roles");

        // Act
        commandCounter.Reset();
        bool coldResult = await HasPermissionAsync(userId, PermissionCodes.Organizations.Manage);
        int coldAuthorizationQueries = CountAuthorizationGrantQueries(commandCounter.GetCommandTexts());

        commandCounter.Reset();
        bool warmResult = await HasPermissionAsync(userId, PermissionCodes.Organizations.Manage);
        int warmAuthorizationQueries = CountAuthorizationGrantQueries(commandCounter.GetCommandTexts());

        // Assert
        coldResult.ShouldBeTrue();
        warmResult.ShouldBeTrue();
        coldAuthorizationQueries.ShouldBe(1);
        warmAuthorizationQueries.ShouldBe(0);
    }

    [Fact]
    public async Task RolePermissionRevocation_ShouldTakeEffectImmediatelyAfterSaveChangesInvalidatesCache()
    {
        // Arrange
        (Guid userId, Guid roleId) = await CreateUserWithRoleAndPermissionAsync(PermissionCodes.Organizations.Manage);
        (await HasPermissionAsync(userId, PermissionCodes.Organizations.Manage)).ShouldBeTrue();

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            RolePermission rolePermission = await context.RolePermissions.SingleAsync(item =>
                item.RoleId == roleId && item.PermissionId == WellKnownPermissions.OrganizationsManageId);
            context.RolePermissions.Remove(rolePermission);
            await context.SaveChangesAsync();
        }

        // Act
        bool isAuthorizedAfterRevocation = await HasPermissionAsync(userId, PermissionCodes.Organizations.Manage);

        // Assert
        isAuthorizedAfterRevocation.ShouldBeFalse();
    }

    [Fact]
    public async Task DatabaseAuthorizationVersion_ShouldBypassStaleCache_WhenLocalInvalidationDoesNotRun()
    {
        (Guid userId, Guid roleId) = await CreateUserWithRoleAndPermissionAsync(
            PermissionCodes.Organizations.Manage);
        (await HasPermissionAsync(userId, PermissionCodes.Organizations.Manage)).ShouldBeTrue();

        // Execute SQL directly to model another application instance or an operational change.
        // The current process receives no HybridCache tag invalidation.
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            int affectedRows = await context.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM public.role_permissions
                WHERE role_id = {roleId}
                  AND permission_id = {WellKnownPermissions.OrganizationsManageId}
                """);
            affectedRows.ShouldBe(1);
        }

        bool isAuthorizedAfterExternalRevocation = await HasPermissionAsync(
            userId,
            PermissionCodes.Organizations.Manage);

        isAuthorizedAfterExternalRevocation.ShouldBeFalse();
    }

    [Fact]
    public async Task DatabaseAuthorizationVersion_ShouldInvalidateTwoIndependentApplicationCaches()
    {
        (Guid userId, Guid roleId) = await CreateUserWithRoleAndPermissionAsync(
            PermissionCodes.Organizations.Manage);
        using WebApplicationFactory<Program> sibling = factory.CreateSiblingFactory();

        (await HasPermissionAsync(factory.Services, userId, PermissionCodes.Organizations.Manage))
            .ShouldBeTrue();
        (await HasPermissionAsync(sibling.Services, userId, PermissionCodes.Organizations.Manage))
            .ShouldBeTrue();

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            int affectedRows = await context.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM public.role_permissions
                WHERE role_id = {roleId}
                  AND permission_id = {WellKnownPermissions.OrganizationsManageId}
                """);
            affectedRows.ShouldBe(1);
        }

        (await HasPermissionAsync(factory.Services, userId, PermissionCodes.Organizations.Manage))
            .ShouldBeFalse();
        (await HasPermissionAsync(sibling.Services, userId, PermissionCodes.Organizations.Manage))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task NonAuthorizationUserUpdate_Should_NotAdvanceGlobalAuthorizationVersion()
    {
        Guid userId = await CreateUserAsync();

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        long versionBefore = await ReadAuthorizationVersionAsync(context);

        int affectedRows = await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE public.users
            SET last_login_utc = CURRENT_TIMESTAMP
            WHERE id = {userId}
            """);
        long versionAfter = await ReadAuthorizationVersionAsync(context);

        affectedRows.ShouldBe(1);
        versionAfter.ShouldBe(versionBefore);
    }

    [Fact]
    public async Task ScopeReplacement_ShouldTakeEffectImmediatelyAfterSaveChangesInvalidatesCache()
    {
        var originalWarehouseId = Guid.NewGuid();
        var replacementWarehouseId = Guid.NewGuid();
        (Guid userId, Guid roleId) = await CreateUserWithRoleAndPermissionAsync(
            PermissionCodes.Warehouses.View,
            ScopeType.Warehouse,
            originalWarehouseId);

        (await HasPermissionInScopeAsync(
            userId,
            PermissionCodes.Warehouses.View,
            ScopeType.Warehouse,
            originalWarehouseId)).ShouldBeTrue();

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            UserRoleScope assignment = await context.UserRoleScopes.SingleAsync(item => item.UserId == userId);
            assignment.ReplaceAssignment(roleId, ScopeType.Warehouse, replacementWarehouseId);
            await context.SaveChangesAsync();
        }

        // Act
        bool hasOriginalWarehouseScope = await HasPermissionInScopeAsync(
            userId,
            PermissionCodes.Warehouses.View,
            ScopeType.Warehouse,
            originalWarehouseId);
        bool hasReplacementWarehouseScope = await HasPermissionInScopeAsync(
            userId,
            PermissionCodes.Warehouses.View,
            ScopeType.Warehouse,
            replacementWarehouseId);

        // Assert
        hasOriginalWarehouseScope.ShouldBeFalse();
        hasReplacementWarehouseScope.ShouldBeTrue();
    }

    [Fact]
    public async Task CachedAuthorization_ShouldRemainIsolatedBetweenUsers()
    {
        Guid permittedUserId = await CreateUserWithPermissionAsync(PermissionCodes.Organizations.Manage);
        Guid unpermittedUserId = await CreateUserAsync();

        // Act
        bool permittedUserCanManage = await HasPermissionAsync(
            permittedUserId,
            PermissionCodes.Organizations.Manage);
        bool unpermittedUserCanManage = await HasPermissionAsync(
            unpermittedUserId,
            PermissionCodes.Organizations.Manage);

        // Assert
        permittedUserCanManage.ShouldBeTrue();
        unpermittedUserCanManage.ShouldBeFalse();
    }

    [Fact]
    public async Task UserSuspension_ShouldInvalidateCachedPermissionsImmediately()
    {
        Guid userId = await CreateUserWithPermissionAsync(PermissionCodes.Organizations.Manage);
        (await HasPermissionAsync(userId, PermissionCodes.Organizations.Manage)).ShouldBeTrue();

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            User user = await context.Users.SingleAsync(item => item.Id == userId);
            user.SetStatus(UserStatus.Suspended);
            await context.SaveChangesAsync();
        }

        (await HasPermissionAsync(userId, PermissionCodes.Organizations.Manage)).ShouldBeFalse();
    }

    [Fact]
    public async Task AuthorizationCacheMetrics_ShouldUseOnlyFixedKeyTypeTagsAndReflectFactoryExecution()
    {
        Guid userId = await CreateUserWithPermissionAsync(PermissionCodes.Organizations.Manage);
        HybridCache cache = factory.Services.GetRequiredService<HybridCache>();
        await cache.RemoveByTagAsync("auth-roles");

        var measurements = new List<(string InstrumentName, long Value, string? KeyType, int TagCount)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == AuthorizationMeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            string? keyType = null;
            foreach (KeyValuePair<string, object?> tag in tags)
            {
                if (tag.Key == "key_type")
                {
                    keyType = tag.Value as string;
                    break;
                }
            }

            measurements.Add((instrument.Name, measurement, keyType, tags.Length));
        });
        listener.Start();

        await HasPermissionAsync(userId, PermissionCodes.Organizations.Manage);
        await HasPermissionAsync(userId, PermissionCodes.Organizations.Manage);

        measurements.Any(item =>
            item.InstrumentName == "authorization.cache.misses" &&
            item.Value == 1 &&
            item.KeyType == "user_grants" &&
            item.TagCount == 1).ShouldBeTrue();
        measurements.Any(item =>
            item.InstrumentName == "authorization.cache.factory_executions" &&
            item.Value == 1 &&
            item.KeyType == "user_grants" &&
            item.TagCount == 1).ShouldBeTrue();
        measurements.Any(item =>
            item.InstrumentName == "authorization.cache.hits" &&
            item.Value == 1 &&
            item.KeyType == "user_grants" &&
            item.TagCount == 1).ShouldBeTrue();
    }

    private async Task<bool> HasPermissionAsync(Guid userId, string permission)
        => await HasPermissionAsync(factory.Services, userId, permission);

    private static async Task<bool> HasPermissionAsync(
        IServiceProvider services,
        Guid userId,
        string permission)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        IScopeAuthorizationService authorization = scope.ServiceProvider.GetRequiredService<IScopeAuthorizationService>();
        return await authorization.HasPermissionAsync(userId, permission, CancellationToken.None);
    }

    private async Task<bool> HasPermissionInScopeAsync(
        Guid userId,
        string permission,
        ScopeType scopeType,
        Guid scopeId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IScopeAuthorizationService authorization = scope.ServiceProvider.GetRequiredService<IScopeAuthorizationService>();
        return await authorization.HasPermissionInScopeAsync(
            userId,
            permission,
            scopeType,
            scopeId,
            CancellationToken.None);
    }

    private async Task<Guid> CreateUserWithPermissionAsync(string permission) =>
        (await CreateUserWithRoleAndPermissionAsync(permission)).UserId;

    private async Task<(Guid UserId, Guid RoleId)> CreateUserWithRoleAndPermissionAsync(
        string permission,
        ScopeType scopeType = ScopeType.Enterprise,
        Guid? scopeId = null)
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        Guid permissionId = GetPermissionId(permission);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.AddRange(
            User.Create(userId, $"authorization-{userId:N}@example.com", "Authorization", "User", "hash"),
            Role.Create(roleId, $"Authorization role {roleId:N}", null),
            RolePermission.Create(roleId, permissionId),
            UserRoleScope.Create(Guid.NewGuid(), userId, roleId, scopeType, scopeId));
        await context.SaveChangesAsync();

        return (userId, roleId);
    }

    private async Task<Guid> CreateUserAsync()
    {
        var userId = Guid.NewGuid();
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Users.Add(User.Create(
            userId,
            $"authorization-{userId:N}@example.com",
            "Authorization",
            "User",
            "hash"));
        await context.SaveChangesAsync();
        return userId;
    }

    private static Task<long> ReadAuthorizationVersionAsync(ApplicationDbContext context) =>
        context.Database
            .SqlQueryRaw<long>("SELECT version AS \"Value\" FROM public.authorization_versions WHERE id = 1")
            .SingleAsync();

    private static Guid GetPermissionId(string permission) => permission switch
    {
        PermissionCodes.Organizations.Manage => WellKnownPermissions.OrganizationsManageId,
        PermissionCodes.Warehouses.View => WellKnownPermissions.WarehousesViewId,
        _ => throw new ArgumentOutOfRangeException(nameof(permission), permission, null)
    };

    private static int CountAuthorizationGrantQueries(IReadOnlyList<string> commandTexts) =>
        commandTexts.Count(command =>
            command.Contains("user_role_scopes", StringComparison.OrdinalIgnoreCase) &&
            command.Contains("role_permissions", StringComparison.OrdinalIgnoreCase) &&
            command.Contains("permissions", StringComparison.OrdinalIgnoreCase));
}
