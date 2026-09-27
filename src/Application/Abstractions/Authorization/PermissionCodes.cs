namespace Application.Abstractions.Authorization;

/// <summary>
/// Canonical permission codes. Defined in Application (not Web.Api) because handlers need the same
/// string as the endpoint's <c>.HasPermission(...)</c> gate for the finer-grained
/// <see cref="IScopeAuthorizationService"/> check - duplicating the literal in both layers invites drift.
/// </summary>
public static class PermissionCodes
{
    public static class Users
    {
        public const string Access = "admin.user.view";
        public const string Manage = "admin.user.manage";
    }

    public static class Organizations
    {
        public const string View = "organization.view";
        public const string Manage = "organization.manage";
    }

    public static class Sites
    {
        public const string View = "organization.view";
        public const string Manage = "organization.manage";
    }

    public static class OrganizationalUnits
    {
        public const string View = "organization.view";
        public const string Manage = "organization.manage";
    }

    public static class Employees
    {
        public const string View = "organization.view";
        public const string Manage = "organization.manage";
    }

    public static class Roles
    {
        public const string View = "admin.role.view";
        public const string Manage = "admin.role.manage";
    }

    public static class UnitsOfMeasure
    {
        public const string View = "catalog.view";
        public const string Manage = "catalog.manage";
    }

    public static class MaterialDomains
    {
        public const string Manage = "catalog.manage";
    }

    public static class MaterialCategories
    {
        public const string Manage = "catalog.manage";
    }

    public static class MaterialFamilies
    {
        public const string Manage = "catalog.manage";
    }

    public static class Materials
    {
        public const string View = "catalog.view";
        public const string Manage = "catalog.manage";
    }

    public static class Warehouses
    {
        public const string View = "warehouse.view";
        public const string Manage = "warehouse.manage";
    }

    public static class Inventory
    {
        public const string View = "inventory.view";
    }

    public static class Assets
    {
        public const string View = "asset.view";
    }

    public static class Custodies
    {
        public const string View = "asset.view";
        public const string Manage = "custody.assign";
    }

    public static class WarehouseCapabilities
    {
        public const string Manage = "warehouse.manage";
    }

    public static class WarehouseMaterialSettings
    {
        public const string Manage = "warehouse.manage";
    }

    public static class WarehouseDocuments
    {
        public const string View = "document.view";
        public const string Create = "document.create";
        public const string Edit = "document.update";
        public const string Submit = "document.submit";
        public const string Cancel = "document.cancel";
        public const string Reverse = "document.reverse";
        public const string Post = "document.post";
        public const string Reject = "document.reject";
        public const string Revise = "document.revise";
    }

    public static class InventoryCounts
    {
        public const string View = "count.view";
        public const string Plan = "count.plan";
        public const string EnterActual = "count.enter";
        public const string Complete = "count.complete";
        public const string Close = "count.close";
    }

    public static class AuditLogs
    {
        public const string View = "audit.view";
    }

    public static class Reports
    {
        public const string View = "report.view";
    }
}
