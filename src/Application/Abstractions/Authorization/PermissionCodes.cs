namespace Application.Abstractions.Authorization;

/// <summary>
/// Canonical permission codes. Defined in Application (not Web.Api) because handlers need the same
/// string as the endpoint's <c>.HasPermission(...)</c> gate for the finer-grained
/// <see cref="IScopeAuthorizationService"/> check - duplicating the literal in both layers invites drift.
/// </summary>
public static class PermissionCodes
{
    public static class Organizations
    {
        public const string Manage = "organizations:manage";
    }

    public static class Sites
    {
        public const string Manage = "sites:manage";
    }

    public static class OrganizationalUnits
    {
        public const string Manage = "org-units:manage";
    }

    public static class Employees
    {
        public const string Manage = "employees:manage";
    }

    public static class Roles
    {
        public const string Manage = "roles:manage";
    }

    public static class UnitsOfMeasure
    {
        public const string Manage = "units-of-measure:manage";
    }

    public static class MaterialDomains
    {
        public const string Manage = "material-domains:manage";
    }

    public static class MaterialCategories
    {
        public const string Manage = "material-categories:manage";
    }

    public static class MaterialFamilies
    {
        public const string Manage = "material-families:manage";
    }

    public static class Materials
    {
        public const string Manage = "materials:manage";
    }

    public static class Warehouses
    {
        public const string Manage = "warehouses:manage";
    }

    public static class WarehouseCapabilities
    {
        public const string Manage = "warehouse-capabilities:manage";
    }

    public static class WarehouseMaterialSettings
    {
        public const string Manage = "warehouse-material-settings:manage";
    }

    public static class WarehouseDocuments
    {
        public const string View = "warehouse-documents:view";
        public const string Create = "warehouse-documents:create";
        public const string Edit = "warehouse-documents:edit";
        public const string Submit = "warehouse-documents:submit";
        public const string Cancel = "warehouse-documents:cancel";
        public const string Review = "warehouse-documents:review";
        public const string Reverse = "warehouse-documents:reverse";
    }

    public static class InventoryCounts
    {
        public const string View = "inventory-counts:view";
        public const string Plan = "inventory-counts:plan";
        public const string EnterActual = "inventory-counts:enter-actual";
        public const string Review = "inventory-counts:review";
    }
}
