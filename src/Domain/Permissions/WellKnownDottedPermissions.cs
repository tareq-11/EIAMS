namespace Domain.Permissions;

/// <summary>
/// Stable identifiers for the v1 dotted authorization catalog. These identifiers are
/// intentionally distinct from the legacy colon permission identifiers so both catalogs
/// can coexist during the expand-only phase.
/// </summary>
public static class WellKnownDottedPermissions
{
    public static readonly Guid AssetViewId = Guid.Parse("00000000-0000-0000-0000-000000000201");
    public static readonly Guid AuditViewId = Guid.Parse("00000000-0000-0000-0000-000000000202");
    public static readonly Guid CustodyAssignId = Guid.Parse("00000000-0000-0000-0000-000000000203");
    public static readonly Guid OrganizationViewId = Guid.Parse("00000000-0000-0000-0000-000000000204");
    public static readonly Guid OrganizationManageId = Guid.Parse("00000000-0000-0000-0000-000000000205");
    public static readonly Guid AdminRoleViewId = Guid.Parse("00000000-0000-0000-0000-000000000206");
    public static readonly Guid AdminRoleManageId = Guid.Parse("00000000-0000-0000-0000-000000000207");
    public static readonly Guid CatalogManageId = Guid.Parse("00000000-0000-0000-0000-000000000208");
    public static readonly Guid CatalogViewId = Guid.Parse("00000000-0000-0000-0000-000000000209");
    public static readonly Guid WarehouseManageId = Guid.Parse("00000000-0000-0000-0000-000000000210");
    public static readonly Guid WarehouseViewId = Guid.Parse("00000000-0000-0000-0000-000000000211");
    public static readonly Guid InventoryViewId = Guid.Parse("00000000-0000-0000-0000-000000000212");
    public static readonly Guid DocumentViewId = Guid.Parse("00000000-0000-0000-0000-000000000213");
    public static readonly Guid DocumentCreateId = Guid.Parse("00000000-0000-0000-0000-000000000214");
    public static readonly Guid DocumentUpdateId = Guid.Parse("00000000-0000-0000-0000-000000000215");
    public static readonly Guid DocumentSubmitId = Guid.Parse("00000000-0000-0000-0000-000000000216");
    public static readonly Guid DocumentPostId = Guid.Parse("00000000-0000-0000-0000-000000000217");
    public static readonly Guid DocumentRejectId = Guid.Parse("00000000-0000-0000-0000-000000000218");
    public static readonly Guid DocumentCancelId = Guid.Parse("00000000-0000-0000-0000-000000000219");
    public static readonly Guid DocumentReverseId = Guid.Parse("00000000-0000-0000-0000-000000000220");
    public static readonly Guid CountViewId = Guid.Parse("00000000-0000-0000-0000-000000000221");
    public static readonly Guid CountPlanId = Guid.Parse("00000000-0000-0000-0000-000000000222");
    public static readonly Guid CountEnterId = Guid.Parse("00000000-0000-0000-0000-000000000223");
    public static readonly Guid CountCompleteId = Guid.Parse("00000000-0000-0000-0000-000000000224");
    public static readonly Guid CountCloseId = Guid.Parse("00000000-0000-0000-0000-000000000225");
    public static readonly Guid AdminUserViewId = Guid.Parse("00000000-0000-0000-0000-000000000226");
    public static readonly Guid AdminUserManageId = Guid.Parse("00000000-0000-0000-0000-000000000227");
    public static readonly Guid DocumentReviseId = Guid.Parse("00000000-0000-0000-0000-000000000228");
    public static readonly Guid ReportViewId = Guid.Parse("00000000-0000-0000-0000-000000000229");

    public static IReadOnlyList<Guid> SystemAdministratorPermissionIds { get; } =
    [
        CatalogViewId,
        CatalogManageId,
        OrganizationViewId,
        OrganizationManageId,
        WarehouseViewId,
        WarehouseManageId,
        AdminUserViewId,
        AdminUserManageId,
        AdminRoleViewId,
        AdminRoleManageId
    ];
}
