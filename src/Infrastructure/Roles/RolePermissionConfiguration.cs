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

        // The Administrator role gets every permission that exists today. WH_KEEPER/WH_MGR were
        // seeded with none through M2 - their grants arrive here with the document workflow they
        // gate (D-WF-01): WH_KEEPER creates/edits/submits/cancels, WH_MGR reviews (posts/rejects)
        // and authorizes reversal; both can view.
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
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownPermissions.WarehouseDocumentsViewId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownPermissions.WarehouseDocumentsCreateId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownPermissions.WarehouseDocumentsEditId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownPermissions.WarehouseDocumentsSubmitId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownPermissions.WarehouseDocumentsCancelId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownPermissions.InventoryCountsViewId },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, PermissionId = WellKnownPermissions.InventoryCountsEnterActualId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownPermissions.WarehouseDocumentsViewId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownPermissions.WarehouseDocumentsReviewId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownPermissions.WarehouseDocumentsReverseId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownPermissions.InventoryCountsViewId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownPermissions.InventoryCountsPlanId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownPermissions.InventoryCountsEnterActualId },
            new { RoleId = WellKnownRoles.WarehouseManagerId, PermissionId = WellKnownPermissions.InventoryCountsReviewId });
    }
}
