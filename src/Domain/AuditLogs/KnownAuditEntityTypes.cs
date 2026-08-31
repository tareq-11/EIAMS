namespace Domain.AuditLogs;

/// <summary>
/// The stable public names of entity types that may be referenced by an audit log entry. Values
/// mirror the aggregate/entity registry of the PRD; audit rows reference entities by name + id so
/// the log survives table renames in application code but stays queryable per type.
/// </summary>
public static class KnownAuditEntityTypes
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        "User",
        "Role",
        "RolePermission",
        "RoleAllowedScopeType",
        "UserRoleScope",
        "Organization",
        "Site",
        "OrganizationalUnit",
        "Employee",
        "ExternalParty",
        "UnitOfMeasure",
        "MaterialDomain",
        "MaterialCategory",
        "MaterialFamily",
        "Material",
        "MaterialUnitConversion",
        "Warehouse",
        "WarehouseCapability",
        "WarehouseCapabilityOperation",
        "WarehouseMaterialSetting",
        "WarehouseDocument",
        "DocumentLine",
        "DocumentAttachment",
        "StockMovement",
        "InventoryBalance",
        "Asset",
        "AssetMovementHistory",
        "Custody",
        "CustodyHistory",
        "DocumentLineAssetSelection",
        "ReceivingInfo",
        "IssueTo",
        "TransferInfo",
        "ReturnInfo",
        "InventoryCount",
        "InventoryCountLine",
        "InventoryCountScopeMaterial",
        "InventoryAdjustment",
        "AdjustmentLine",
        "TrackedMaterialUnit",
        "DurableCustodyAllocation",
        "DurableCustodyHistory",
        "DurableCustody"
    };

    public static bool IsKnown(string entityType) => All.Contains(entityType);
}
