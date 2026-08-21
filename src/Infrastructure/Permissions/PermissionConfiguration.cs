using Application.Abstractions.Authorization;
using Domain.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Permissions;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.Code).IsUnique();

        builder.Property(p => p.Code).HasMaxLength(100);

        builder.HasData(
            new
            {
                Id = WellKnownPermissions.UsersAccessId,
                Code = "users:access",
                Description = (string?)"Access another user's profile."
            },
            new
            {
                Id = WellKnownPermissions.OrganizationsManageId,
                Code = PermissionCodes.Organizations.Manage,
                Description = (string?)"Create, update, and change the status of organizations."
            },
            new
            {
                Id = WellKnownPermissions.SitesManageId,
                Code = PermissionCodes.Sites.Manage,
                Description = (string?)"Create, update, and change the status of sites."
            },
            new
            {
                Id = WellKnownPermissions.OrganizationalUnitsManageId,
                Code = PermissionCodes.OrganizationalUnits.Manage,
                Description = (string?)"Create, update, and change the status of organizational units."
            },
            new
            {
                Id = WellKnownPermissions.EmployeesManageId,
                Code = PermissionCodes.Employees.Manage,
                Description = (string?)"Create, update, and change the status of employees."
            },
            new
            {
                Id = WellKnownPermissions.RolesManageId,
                Code = PermissionCodes.Roles.Manage,
                Description = (string?)"Manage roles, role permissions, and user role scope grants."
            },
            new
            {
                Id = WellKnownPermissions.UnitsOfMeasureManageId,
                Code = PermissionCodes.UnitsOfMeasure.Manage,
                Description = (string?)"Create and update units of measure."
            },
            new
            {
                Id = WellKnownPermissions.MaterialDomainsManageId,
                Code = PermissionCodes.MaterialDomains.Manage,
                Description = (string?)"Create, update, and change the status of material domains."
            },
            new
            {
                Id = WellKnownPermissions.MaterialCategoriesManageId,
                Code = PermissionCodes.MaterialCategories.Manage,
                Description = (string?)"Create, update, and change the status of material categories."
            },
            new
            {
                Id = WellKnownPermissions.MaterialFamiliesManageId,
                Code = PermissionCodes.MaterialFamilies.Manage,
                Description = (string?)"Create, update, and change the status of material families."
            },
            new
            {
                Id = WellKnownPermissions.MaterialsManageId,
                Code = PermissionCodes.Materials.Manage,
                Description = (string?)"Create, update, and change the status of materials and their unit conversions."
            },
            new
            {
                Id = WellKnownPermissions.WarehousesManageId,
                Code = PermissionCodes.Warehouses.Manage,
                Description = (string?)"Create, update, and change the status of warehouses."
            },
            new
            {
                Id = WellKnownPermissions.WarehouseCapabilitiesManageId,
                Code = PermissionCodes.WarehouseCapabilities.Manage,
                Description = (string?)"Grant, revoke, and configure the operations of warehouse capabilities."
            },
            new
            {
                Id = WellKnownPermissions.WarehouseMaterialSettingsManageId,
                Code = PermissionCodes.WarehouseMaterialSettings.Manage,
                Description = (string?)"Create, update, and change the status of warehouse material settings."
            },
            new
            {
                Id = WellKnownPermissions.WarehouseDocumentsViewId,
                Code = PermissionCodes.WarehouseDocuments.View,
                Description = (string?)"View warehouse documents, lines, attachments, and the ledger."
            },
            new
            {
                Id = WellKnownPermissions.WarehouseDocumentsCreateId,
                Code = PermissionCodes.WarehouseDocuments.Create,
                Description = (string?)"Create warehouse documents."
            },
            new
            {
                Id = WellKnownPermissions.WarehouseDocumentsEditId,
                Code = PermissionCodes.WarehouseDocuments.Edit,
                Description = (string?)"Edit a Draft warehouse document: lines, paper reference, and attachments."
            },
            new
            {
                Id = WellKnownPermissions.WarehouseDocumentsSubmitId,
                Code = PermissionCodes.WarehouseDocuments.Submit,
                Description = (string?)"Submit a Draft warehouse document for review."
            },
            new
            {
                Id = WellKnownPermissions.WarehouseDocumentsCancelId,
                Code = PermissionCodes.WarehouseDocuments.Cancel,
                Description = (string?)"Cancel a warehouse document before it is posted."
            },
            new
            {
                Id = WellKnownPermissions.WarehouseDocumentsReviewId,
                Code = PermissionCodes.WarehouseDocuments.Review,
                Description = (string?)"Post or reject a submitted warehouse document."
            },
            new
            {
                Id = WellKnownPermissions.WarehouseDocumentsReverseId,
                Code = PermissionCodes.WarehouseDocuments.Reverse,
                Description = (string?)"Authorize posting a reversal of a posted warehouse document."
            },
            new
            {
                Id = WellKnownPermissions.InventoryCountsViewId,
                Code = PermissionCodes.InventoryCounts.View,
                Description = (string?)"View warehouse inventory counts and freeze status."
            },
            new
            {
                Id = WellKnownPermissions.InventoryCountsPlanId,
                Code = PermissionCodes.InventoryCounts.Plan,
                Description = (string?)"Plan inventory counts and capture snapshots."
            },
            new
            {
                Id = WellKnownPermissions.InventoryCountsEnterActualId,
                Code = PermissionCodes.InventoryCounts.EnterActual,
                Description = (string?)"Enter actual quantities during inventory counts."
            },
            new
            {
                Id = WellKnownPermissions.InventoryCountsReviewId,
                Code = PermissionCodes.InventoryCounts.Review,
                Description = (string?)"Start, complete, explain, and close inventory counts."
            });
    }
}
