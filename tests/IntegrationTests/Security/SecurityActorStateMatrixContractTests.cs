using System.Reflection;
using IntegrationTests.Authorization;
using IntegrationTests.M0M1;
using IntegrationTests.Phase10;
using IntegrationTests.Users;

namespace IntegrationTests.Security;

/// <summary>
/// Executable drift/index contract for the v1 actor/state matrix. It does not execute the
/// referenced tests; CI's full integration suite executes those concrete HTTP facts. This
/// contract fails if a matrix row loses its named executable evidence.
/// </summary>
public sealed class SecurityActorStateMatrixContractTests
{
    public static TheoryData<string, Type, string> Rows => new()
    {
        { "anonymous_protected_request_401", typeof(ScopeEnforcementTests), "ProtectedEndpoint_Should_Return401_WhenNotAuthenticated" },
        { "authenticated_without_permission_403", typeof(InventoryReadApiIntegrationTests), "GlobalInventoryReads_Should_ReturnForbidden_WhenUserHasNoInventoryScopePermission" },
        { "viewer_reads_and_cannot_mutate", typeof(ScopeEnforcementTests), "WarehouseScopedReadPermissions_Should_AuthorizeMatchingReadEndpointsOnly" },
        { "site_scope_mutates_descendant_warehouse", typeof(M0M1AuthorizationAndDatabaseTests), "UpdateWarehouse_Should_AuthorizeSiteGrantForWarehouseInsideThatSite" },
        { "editor_cannot_mutate_outside_scope", typeof(M0M1AuthorizationAndDatabaseTests), "UpdateWarehouse_Should_ReturnForbidden_WhenSiteGrantTargetsAnotherSite" },
        { "scoped_editor_cannot_perform_admin_mutation", typeof(M0M1AuthorizationAndDatabaseTests), "ScopedWarehouseEditor_Should_NotPerformAdministratorUserMutation" },
        { "warehouse_scope_denies_outside_resource", typeof(ScopeEnforcementTests), "WarehouseScopedReadPermissions_Should_AuthorizeMatchingReadEndpointsOnly" },
        { "organizational_unit_scope_mutates_descendant_and_rejects_sibling", typeof(M0M1AuthorizationAndDatabaseTests), "UpdateWarehouse_Should_AuthorizeOrganizationalUnitDescendantsButRejectSibling" },
        { "service_scope_expansion_no_cross_scope_leakage", typeof(HierarchicalScopeQueryIntegrationTests), "WarehousePermissionScope_ExpandsEachSingleAssignmentWithoutCrossScopeLeakage" },
        { "enterprise_administrator_admin_operation", typeof(UserAdministrationIntegrationTests), "AdministrationWorkflow_Should_CreateListSuspendAndBlockAllAuthenticationPaths" },
        { "suspended_user_and_token_rejected", typeof(UserAdministrationIntegrationTests), "AdministrationWorkflow_Should_CreateListSuspendAndBlockAllAuthenticationPaths" },
        { "revoked_refresh_replay_rejected", typeof(UsersTests), "ConcurrentRefresh_Should_AllowOnlyOneRotation_AndInvalidateTheTokenFamilyAsReplay" },
        { "expired_jwt_rejected", typeof(JwtConfigurationTests), "ExpiredSignedToken_Should_Return401Unauthorized" }
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void EveryActorStateRow_ShouldReferenceAnExecutableIntegrationFact(
        string row,
        Type testType,
        string testMethod)
    {
        MethodInfo? method = testType.GetMethod(testMethod, BindingFlags.Instance | BindingFlags.Public);

        method.ShouldNotBeNull($"Matrix row '{row}' has no integration evidence.");
        method.GetCustomAttributes(inherit: true).OfType<FactAttribute>().Any().ShouldBeTrue(
            $"Matrix row '{row}' must reference an executable xUnit fact.");
    }
}
