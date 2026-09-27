namespace Application.Abstractions.Authorization;

/// <summary>
/// Exact permission vocabularies accepted by an authorization policy marker.
/// Dormant target codes may coexist in the catalog, but only the marker-selected set is effective.
/// </summary>
public static class PermissionVocabulary
{
    public static readonly string[] LegacyColonCodes =
    [
        "users:access", "organizations:view", "organizations:manage", "sites:view", "sites:manage",
        "org-units:view", "org-units:manage", "employees:view", "employees:manage", "roles:view",
        "roles:manage", "units-of-measure:view", "units-of-measure:manage", "material-domains:manage",
        "material-categories:manage", "material-families:manage", "materials:view", "materials:manage",
        "warehouses:view", "warehouses:manage", "inventory:view", "assets:view", "custody:view",
        "custody:manage", "warehouse-capabilities:manage", "warehouse-material-settings:manage",
        "warehouse-documents:view", "warehouse-documents:create", "warehouse-documents:edit",
        "warehouse-documents:submit", "warehouse-documents:cancel", "warehouse-documents:review",
        "warehouse-documents:reverse", "inventory-counts:view", "inventory-counts:plan",
        "inventory-counts:enter-actual", "inventory-counts:review", "audit-logs:view"
    ];

    public static readonly string[] DottedV1Codes =
    [
        "asset.view", "audit.view", "custody.assign", "organization.view", "organization.manage",
        "admin.role.view", "admin.role.manage", "catalog.manage", "catalog.view", "warehouse.manage",
        "warehouse.view", "inventory.view", "document.view", "document.create", "document.update",
        "document.submit", "document.cancel", "document.post", "document.reject", "document.reverse",
        "count.view", "count.plan", "count.enter", "count.complete", "count.close",
        "admin.user.view", "admin.user.manage", "document.revise", "report.view"
    ];
}
