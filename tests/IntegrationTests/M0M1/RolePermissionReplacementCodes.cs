namespace IntegrationTests.M0M1;

/// <summary>
/// Shared permission-code arrays for the role permission-replacement tests, kept out of the test
/// bodies so the analyzer does not flag a constant array argument on every call.
/// </summary>
internal static class RolePermissionReplacementCodes
{
    internal static readonly string[] CatalogView = ["catalog.view"];

    internal static readonly string[] CatalogManage = ["catalog.manage"];

    internal static readonly string[] CatalogViewAndManage = ["catalog.manage", "catalog.view"];

    /// <summary>An explicitly empty set: a valid request that revokes every grant.</summary>
    internal static readonly string[] None = [];
}