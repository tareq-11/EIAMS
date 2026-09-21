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
                Code = "organizations:manage",
                Description = (string?)"Create, update, and change the status of organizations."
            },
            new
            {
                Id = WellKnownPermissions.SitesManageId,
                Code = "sites:manage",
                Description = (string?)"Create, update, and change the status of sites."
            },
            new
            {
                Id = WellKnownPermissions.OrganizationalUnitsManageId,
                Code = "org-units:manage",
                Description = (string?)"Create, update, and change the status of organizational units."
            },
            new
            {
                Id = WellKnownPermissions.EmployeesManageId,
                Code = "employees:manage",
                Description = (string?)"Create, update, and change the status of employees."
            },
            new
            {
                Id = WellKnownPermissions.RolesManageId,
                Code = "roles:manage",
                Description = (string?)"Manage roles, role permissions, and user role scope grants."
            },
            new
            {
                Id = WellKnownPermissions.UnitsOfMeasureManageId,
                Code = "units-of-measure:manage",
                Description = (string?)"Create and update units of measure."
            },
            new
            {
                Id = WellKnownPermissions.MaterialDomainsManageId,
                Code = "material-domains:manage",
                Description = (string?)"Create, update, and change the status of material domains."
            },
            new
            {
                Id = WellKnownPermissions.MaterialCategoriesManageId,
                Code = "material-categories:manage",
                Description = (string?)"Create, update, and change the status of material categories."
            },
            new
            {
                Id = WellKnownPermissions.MaterialFamiliesManageId,
                Code = "material-families:manage",
                Description = (string?)"Create, update, and change the status of material families."
            },
            new
            {
                Id = WellKnownPermissions.MaterialsManageId,
                Code = "materials:manage",
                Description = (string?)"Create, update, and change the status of materials and their unit conversions."
            },
            new
            {
                Id = WellKnownPermissions.WarehousesManageId,
                Code = "warehouses:manage",
                Description = (string?)"Create, update, and change the status of warehouses."
            },
            new
            {
                Id = WellKnownPermissions.WarehouseCapabilitiesManageId,
                Code = "warehouse-capabilities:manage",
                Description = (string?)"Grant, revoke, and configure the operations of warehouse capabilities."
            },
            new
            {
                Id = WellKnownPermissions.WarehouseMaterialSettingsManageId,
                Code = "warehouse-material-settings:manage",
                Description = (string?)"Create, update, and change the status of warehouse material settings."
            },
            new
            {
                Id = WellKnownPermissions.WarehouseDocumentsViewId,
                Code = "warehouse-documents:view",
                Description = (string?)"View warehouse documents, lines, attachments, and the ledger."
            },
            new
            {
                Id = WellKnownPermissions.WarehouseDocumentsCreateId,
                Code = "warehouse-documents:create",
                Description = (string?)"Create warehouse documents."
            },
            new
            {
                Id = WellKnownPermissions.WarehouseDocumentsEditId,
                Code = "warehouse-documents:edit",
                Description = (string?)"Edit a Draft warehouse document: lines, paper reference, and attachments."
            },
            new
            {
                Id = WellKnownPermissions.WarehouseDocumentsSubmitId,
                Code = "warehouse-documents:submit",
                Description = (string?)"Submit a Draft warehouse document for review."
            },
            new
            {
                Id = WellKnownPermissions.WarehouseDocumentsCancelId,
                Code = "warehouse-documents:cancel",
                Description = (string?)"Cancel a warehouse document before it is posted."
            },
            new
            {
                Id = WellKnownPermissions.WarehouseDocumentsReviewId,
                Code = "warehouse-documents:review",
                Description = (string?)"Post or reject a submitted warehouse document."
            },
            new
            {
                Id = WellKnownPermissions.WarehouseDocumentsReverseId,
                Code = "warehouse-documents:reverse",
                Description = (string?)"Authorize posting a reversal of a posted warehouse document."
            },
            new
            {
                Id = WellKnownPermissions.InventoryCountsViewId,
                Code = "inventory-counts:view",
                Description = (string?)"View warehouse inventory counts and freeze status."
            },
            new
            {
                Id = WellKnownPermissions.InventoryCountsPlanId,
                Code = "inventory-counts:plan",
                Description = (string?)"Plan inventory counts and capture snapshots."
            },
            new
            {
                Id = WellKnownPermissions.InventoryCountsEnterActualId,
                Code = "inventory-counts:enter-actual",
                Description = (string?)"Enter actual quantities during inventory counts."
            },
            new
            {
                Id = WellKnownPermissions.InventoryCountsReviewId,
                Code = "inventory-counts:review",
                Description = (string?)"Start, complete, explain, and close inventory counts."
            },
            new
            {
                Id = WellKnownPermissions.AuditLogsViewId,
                Code = "audit-logs:view",
                Description = (string?)"View the immutable audit trail of system activity."
            },
            new
            {
                Id = WellKnownPermissions.OrganizationsViewId,
                Code = "organizations:view",
                Description = (string?)"View organizations."
            },
            new
            {
                Id = WellKnownPermissions.SitesViewId,
                Code = "sites:view",
                Description = (string?)"View sites."
            },
            new
            {
                Id = WellKnownPermissions.OrganizationalUnitsViewId,
                Code = "org-units:view",
                Description = (string?)"View organizational units."
            },
            new
            {
                Id = WellKnownPermissions.EmployeesViewId,
                Code = "employees:view",
                Description = (string?)"View employees."
            },
            new
            {
                Id = WellKnownPermissions.RolesViewId,
                Code = "roles:view",
                Description = (string?)"View roles and permissions."
            },
            new
            {
                Id = WellKnownPermissions.UnitsOfMeasureViewId,
                Code = "units-of-measure:view",
                Description = (string?)"View units of measure."
            },
            new
            {
                Id = WellKnownPermissions.MaterialsViewId,
                Code = "materials:view",
                Description = (string?)"View the material catalog and unit conversions."
            },
            new
            {
                Id = WellKnownPermissions.WarehousesViewId,
                Code = "warehouses:view",
                Description = (string?)"View warehouses, capabilities, and material settings."
            },
            new
            {
                Id = WellKnownPermissions.InventoryViewId,
                Code = "inventory:view",
                Description = (string?)"View inventory balances and stock movements."
            },
            new
            {
                Id = WellKnownPermissions.AssetsViewId,
                Code = "assets:view",
                Description = (string?)"View asset status."
            },
            new
            {
                Id = WellKnownPermissions.CustodiesViewId,
                Code = "custody:view",
                Description = (string?)"View custody state and history."
            },
            new
            {
                Id = WellKnownPermissions.CustodiesManageId,
                Code = "custody:manage",
                Description = (string?)"Assign and manage asset custody."
            });

        // Dotted v1 catalog. IDs are intentionally new; the legacy colon catalog remains
        // seeded and selected by the active policy marker until coordinated cutover.
        builder.HasData(
            new { Id = WellKnownDottedPermissions.AssetViewId, Code = "asset.view", Description = (string?)"View assets." },
            new { Id = WellKnownDottedPermissions.AuditViewId, Code = "audit.view", Description = (string?)"View audit history." },
            new { Id = WellKnownDottedPermissions.CustodyAssignId, Code = "custody.assign", Description = (string?)"Assign personal custody." },
            new { Id = WellKnownDottedPermissions.OrganizationViewId, Code = "organization.view", Description = (string?)"View organization structure." },
            new { Id = WellKnownDottedPermissions.OrganizationManageId, Code = "organization.manage", Description = (string?)"Manage organization structure." },
            new { Id = WellKnownDottedPermissions.AdminRoleViewId, Code = "admin.role.view", Description = (string?)"View roles and permissions." },
            new { Id = WellKnownDottedPermissions.AdminRoleManageId, Code = "admin.role.manage", Description = (string?)"Manage roles and permissions." },
            new { Id = WellKnownDottedPermissions.CatalogManageId, Code = "catalog.manage", Description = (string?)"Manage the material catalog." },
            new { Id = WellKnownDottedPermissions.CatalogViewId, Code = "catalog.view", Description = (string?)"View the material catalog." },
            new { Id = WellKnownDottedPermissions.WarehouseManageId, Code = "warehouse.manage", Description = (string?)"Manage warehouses and capabilities." },
            new { Id = WellKnownDottedPermissions.WarehouseViewId, Code = "warehouse.view", Description = (string?)"View warehouses and settings." },
            new { Id = WellKnownDottedPermissions.InventoryViewId, Code = "inventory.view", Description = (string?)"View inventory." },
            new { Id = WellKnownDottedPermissions.DocumentViewId, Code = "document.view", Description = (string?)"View warehouse documents." },
            new { Id = WellKnownDottedPermissions.DocumentCreateId, Code = "document.create", Description = (string?)"Create warehouse documents." },
            new { Id = WellKnownDottedPermissions.DocumentUpdateId, Code = "document.update", Description = (string?)"Update warehouse documents." },
            new { Id = WellKnownDottedPermissions.DocumentSubmitId, Code = "document.submit", Description = (string?)"Submit warehouse documents." },
            new { Id = WellKnownDottedPermissions.DocumentPostId, Code = "document.post", Description = (string?)"Post warehouse documents." },
            new { Id = WellKnownDottedPermissions.DocumentRejectId, Code = "document.reject", Description = (string?)"Reject warehouse documents." },
            new { Id = WellKnownDottedPermissions.DocumentCancelId, Code = "document.cancel", Description = (string?)"Cancel warehouse documents." },
            new { Id = WellKnownDottedPermissions.DocumentReverseId, Code = "document.reverse", Description = (string?)"Reverse warehouse documents." },
            new { Id = WellKnownDottedPermissions.CountViewId, Code = "count.view", Description = (string?)"View inventory counts." },
            new { Id = WellKnownDottedPermissions.CountPlanId, Code = "count.plan", Description = (string?)"Plan inventory counts." },
            new { Id = WellKnownDottedPermissions.CountEnterId, Code = "count.enter", Description = (string?)"Enter count actuals." },
            new { Id = WellKnownDottedPermissions.CountCompleteId, Code = "count.complete", Description = (string?)"Complete inventory counts." },
            new { Id = WellKnownDottedPermissions.CountCloseId, Code = "count.close", Description = (string?)"Close inventory counts." },
            new { Id = WellKnownDottedPermissions.AdminUserViewId, Code = "admin.user.view", Description = (string?)"View users." },
            new { Id = WellKnownDottedPermissions.AdminUserManageId, Code = "admin.user.manage", Description = (string?)"Manage users." },
            new { Id = WellKnownDottedPermissions.DocumentReviseId, Code = "document.revise", Description = (string?)"Revise rejected warehouse documents." },
            new { Id = WellKnownDottedPermissions.ReportViewId, Code = "report.view", Description = (string?)"View operational reports." });
    }
}
