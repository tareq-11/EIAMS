using Domain.Common;
using Domain.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Permissions;

internal sealed class PermissionAllowedScopeTypeConfiguration : IEntityTypeConfiguration<PermissionAllowedScopeType>
{
    public void Configure(EntityTypeBuilder<PermissionAllowedScopeType> builder)
    {
        builder.ToTable("permission_allowed_scope_types");
        builder.HasKey(item => new { item.PermissionId, item.ScopeType });
        builder.Property(item => item.ScopeType).HasConversion<string>().HasMaxLength(30);
        builder.HasOne<Permission>().WithMany().HasForeignKey(item => item.PermissionId);
        builder.HasData(PermissionAllowedScopeTypeSeed.Rows());
    }
}

internal static class PermissionAllowedScopeTypeSeed
{
    private static readonly Guid[] ReadPermissions =
    [
        WellKnownPermissions.AuditLogsViewId,
        WellKnownPermissions.OrganizationsViewId,
        WellKnownPermissions.SitesViewId,
        WellKnownPermissions.OrganizationalUnitsViewId,
        WellKnownPermissions.EmployeesViewId,
        WellKnownPermissions.UnitsOfMeasureViewId,
        WellKnownPermissions.MaterialsViewId,
        WellKnownPermissions.WarehousesViewId,
        WellKnownPermissions.InventoryViewId,
        WellKnownPermissions.AssetsViewId,
        WellKnownPermissions.CustodiesViewId,
        WellKnownPermissions.WarehouseDocumentsViewId,
        WellKnownPermissions.InventoryCountsViewId
    ];

    private static readonly Guid[] StructuralManagePermissions =
    [
        WellKnownPermissions.UsersAccessId,
        WellKnownPermissions.OrganizationsManageId,
        WellKnownPermissions.SitesManageId,
        WellKnownPermissions.OrganizationalUnitsManageId,
        WellKnownPermissions.EmployeesManageId,
        WellKnownPermissions.RolesManageId,
        WellKnownPermissions.RolesViewId,
        WellKnownPermissions.UnitsOfMeasureManageId,
        WellKnownPermissions.MaterialDomainsManageId,
        WellKnownPermissions.MaterialCategoriesManageId,
        WellKnownPermissions.MaterialFamiliesManageId,
        WellKnownPermissions.MaterialsManageId,
        WellKnownPermissions.WarehousesManageId,
        WellKnownPermissions.WarehouseCapabilitiesManageId,
        WellKnownPermissions.WarehouseMaterialSettingsManageId
    ];

    private static readonly Guid[] OperationalMutationPermissions =
    [
        WellKnownPermissions.CustodiesManageId,
        WellKnownPermissions.WarehouseDocumentsCreateId,
        WellKnownPermissions.WarehouseDocumentsEditId,
        WellKnownPermissions.WarehouseDocumentsSubmitId,
        WellKnownPermissions.WarehouseDocumentsCancelId,
        WellKnownPermissions.WarehouseDocumentsReviewId,
        WellKnownPermissions.WarehouseDocumentsReverseId,
        WellKnownPermissions.InventoryCountsPlanId,
        WellKnownPermissions.InventoryCountsEnterActualId,
        WellKnownPermissions.InventoryCountsReviewId
    ];

    private static readonly Guid[] DottedGovernanceReads =
    [
        WellKnownDottedPermissions.AssetViewId,
        WellKnownDottedPermissions.AuditViewId,
        WellKnownDottedPermissions.OrganizationViewId,
        WellKnownDottedPermissions.CatalogViewId,
        WellKnownDottedPermissions.WarehouseViewId,
        WellKnownDottedPermissions.InventoryViewId,
        WellKnownDottedPermissions.DocumentViewId,
        WellKnownDottedPermissions.CountViewId,
        WellKnownDottedPermissions.ReportViewId
    ];

    private static readonly Guid[] DottedStructuralManage =
    [
        WellKnownDottedPermissions.OrganizationManageId,
        WellKnownDottedPermissions.CatalogManageId,
        WellKnownDottedPermissions.WarehouseManageId,
        WellKnownDottedPermissions.AdminUserViewId,
        WellKnownDottedPermissions.AdminUserManageId,
        WellKnownDottedPermissions.AdminRoleViewId,
        WellKnownDottedPermissions.AdminRoleManageId
    ];

    private static readonly Guid[] DottedOperationalMutations =
    [
        WellKnownDottedPermissions.CustodyAssignId,
        WellKnownDottedPermissions.DocumentCreateId,
        WellKnownDottedPermissions.DocumentUpdateId,
        WellKnownDottedPermissions.DocumentSubmitId,
        WellKnownDottedPermissions.DocumentPostId,
        WellKnownDottedPermissions.DocumentRejectId,
        WellKnownDottedPermissions.DocumentCancelId,
        WellKnownDottedPermissions.DocumentReverseId,
        WellKnownDottedPermissions.DocumentReviseId,
        WellKnownDottedPermissions.CountPlanId,
        WellKnownDottedPermissions.CountEnterId,
        WellKnownDottedPermissions.CountCompleteId,
        WellKnownDottedPermissions.CountCloseId
    ];

    public static object[] Rows() =>
        ReadPermissions.SelectMany(permissionId => new[]
            {
                new { PermissionId = permissionId, ScopeType = ScopeType.Enterprise },
                new { PermissionId = permissionId, ScopeType = ScopeType.Site },
                new { PermissionId = permissionId, ScopeType = ScopeType.Warehouse }
            })
            .Concat(StructuralManagePermissions.Select(permissionId =>
                new { PermissionId = permissionId, ScopeType = ScopeType.Enterprise }))
            .Concat(OperationalMutationPermissions.Select(permissionId =>
                new { PermissionId = permissionId, ScopeType = ScopeType.Warehouse }))
            .Concat(DottedGovernanceReads.SelectMany(permissionId => new[]
            {
                new { PermissionId = permissionId, ScopeType = ScopeType.Enterprise },
                new { PermissionId = permissionId, ScopeType = ScopeType.Site },
                new { PermissionId = permissionId, ScopeType = ScopeType.Warehouse }
            }))
            .Concat(DottedStructuralManage.Select(permissionId =>
                new { PermissionId = permissionId, ScopeType = ScopeType.Enterprise }))
            .Concat(DottedOperationalMutations.Select(permissionId =>
                new { PermissionId = permissionId, ScopeType = ScopeType.Warehouse }))
            .Cast<object>()
            .ToArray();
}
