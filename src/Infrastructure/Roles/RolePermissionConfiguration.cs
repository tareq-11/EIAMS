using Domain.Permissions;
using Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Roles;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });

        builder.HasOne<Role>().WithMany().HasForeignKey(rp => rp.RoleId);

        builder.HasOne<Permission>().WithMany().HasForeignKey(rp => rp.PermissionId);

        // Legacy grants remain retained for observation/history. Dotted runtime effective
        // permissions are seeded separately below; SYSTEM_ADMIN is deliberately structural-only.
        builder.HasData(
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.UsersAccessId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.OrganizationsManageId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.SitesManageId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.OrganizationalUnitsManageId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.EmployeesManageId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.RolesManageId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.UnitsOfMeasureManageId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.MaterialDomainsManageId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.MaterialCategoriesManageId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.MaterialFamiliesManageId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.MaterialsManageId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.WarehousesManageId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.WarehouseCapabilitiesManageId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.WarehouseMaterialSettingsManageId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.WarehouseDocumentsViewId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.WarehouseDocumentsCreateId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.WarehouseDocumentsEditId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.WarehouseDocumentsSubmitId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.WarehouseDocumentsCancelId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.WarehouseDocumentsReviewId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.WarehouseDocumentsReverseId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.InventoryCountsViewId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.InventoryCountsPlanId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.InventoryCountsEnterActualId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.InventoryCountsReviewId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.AuditLogsViewId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.OrganizationsViewId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.SitesViewId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.OrganizationalUnitsViewId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.EmployeesViewId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.RolesViewId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.UnitsOfMeasureViewId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.MaterialsViewId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.WarehousesViewId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.InventoryViewId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.AssetsViewId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.CustodiesViewId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.CustodiesManageId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownPermissions.WarehouseDocumentsViewId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownPermissions.WarehouseDocumentsCreateId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownPermissions.WarehouseDocumentsEditId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownPermissions.WarehouseDocumentsSubmitId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownPermissions.WarehouseDocumentsCancelId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownPermissions.InventoryCountsViewId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownPermissions.InventoryCountsEnterActualId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownPermissions.MaterialsViewId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownPermissions.WarehousesViewId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownPermissions.InventoryViewId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownPermissions.AssetsViewId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownPermissions.CustodiesViewId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownPermissions.CustodiesManageId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownPermissions.WarehouseDocumentsViewId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownPermissions.WarehouseDocumentsReviewId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownPermissions.WarehouseDocumentsReverseId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownPermissions.InventoryCountsViewId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownPermissions.InventoryCountsPlanId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownPermissions.InventoryCountsEnterActualId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownPermissions.InventoryCountsReviewId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownPermissions.MaterialsViewId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownPermissions.WarehousesViewId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownPermissions.InventoryViewId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownPermissions.AssetsViewId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownPermissions.CustodiesViewId });

        // Dotted v1 grants are expanded alongside the retained legacy grants. Runtime
        // authorization emits only the marker-selected dotted vocabulary after cutover.
        builder.HasData(
            // SYSTEM_ADMIN: structural administration only (no audit/report/inventory operations).
            // WH_MGR: governance everywhere it may be assigned, plus warehouse operations.
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownDottedPermissions.CatalogViewId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownDottedPermissions.OrganizationViewId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownDottedPermissions.WarehouseViewId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownDottedPermissions.InventoryViewId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownDottedPermissions.DocumentViewId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownDottedPermissions.CountViewId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownDottedPermissions.AssetViewId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownDottedPermissions.ReportViewId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownDottedPermissions.DocumentCreateId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownDottedPermissions.DocumentUpdateId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownDottedPermissions.DocumentPostId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownDottedPermissions.DocumentRejectId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownDottedPermissions.DocumentCancelId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownDottedPermissions.DocumentReverseId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownDottedPermissions.CountPlanId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownDottedPermissions.CountCompleteId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownDottedPermissions.CountCloseId },

            // WH_KEEPER: warehouse document preparation, count entry, custody and reporting.
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownDottedPermissions.CatalogViewId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownDottedPermissions.OrganizationViewId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownDottedPermissions.WarehouseViewId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownDottedPermissions.InventoryViewId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownDottedPermissions.DocumentViewId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownDottedPermissions.DocumentCreateId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownDottedPermissions.DocumentUpdateId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownDottedPermissions.DocumentSubmitId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownDottedPermissions.DocumentReviseId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownDottedPermissions.DocumentCancelId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownDottedPermissions.CountViewId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownDottedPermissions.CountEnterId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownDottedPermissions.AssetViewId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownDottedPermissions.CustodyAssignId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownDottedPermissions.ReportViewId },

            // AUDITOR: read-only context, operational views, audit and reporting.
            new { RoleId = WellKnownRoles.AuditorId, PermissionId = WellKnownDottedPermissions.CatalogViewId },
            new { RoleId = WellKnownRoles.AuditorId, PermissionId = WellKnownDottedPermissions.OrganizationViewId },
            new { RoleId = WellKnownRoles.AuditorId, PermissionId = WellKnownDottedPermissions.WarehouseViewId },
            new { RoleId = WellKnownRoles.AuditorId, PermissionId = WellKnownDottedPermissions.InventoryViewId },
            new { RoleId = WellKnownRoles.AuditorId, PermissionId = WellKnownDottedPermissions.DocumentViewId },
            new { RoleId = WellKnownRoles.AuditorId, PermissionId = WellKnownDottedPermissions.CountViewId },
            new { RoleId = WellKnownRoles.AuditorId, PermissionId = WellKnownDottedPermissions.AssetViewId },
            new { RoleId = WellKnownRoles.AuditorId, PermissionId = WellKnownDottedPermissions.AuditViewId },
            new { RoleId = WellKnownRoles.AuditorId, PermissionId = WellKnownDottedPermissions.ReportViewId });

        builder.HasData(WellKnownDottedPermissions.SystemAdministratorPermissionIds
            .Select(permissionId => new
            {
                RoleId = WellKnownRoles.AdministratorId,
                PermissionId = permissionId
            })
            .ToArray());
    }
}
