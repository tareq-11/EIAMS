using Domain.AssetMovementHistories;
using Domain.Assets;
using Domain.AuditLogs;
using Domain.Custodies;
using Domain.CustodyHistories;
using Domain.DocumentAttachments;
using Domain.DocumentLineAssetSelections;
using Domain.DocumentLines;
using Domain.DocumentLifecycleEvents;
using Domain.DocumentSequences;
using Domain.DurableCustodies;
using Domain.DurableCustodyAllocations;
using Domain.Employees;
using Domain.ExternalParties;
using Domain.InventoryAdjustments;
using Domain.InventoryBalances;
using Domain.InventoryCounts;
using Domain.IssueTos;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.MaterialUnitConversions;
using Domain.OrganizationalUnits;
using Domain.Organizations;
using Domain.Permissions;
using Domain.ReceivingInfos;
using Domain.ReturnInfos;
using Domain.Roles;
using Domain.Sites;
using Domain.StockMovements;
using Domain.TrackedMaterialUnits;
using Domain.TransferInfos;
using Domain.UnitsOfMeasure;
using Domain.UserRoleScopes;
using Domain.Users;
using Domain.WarehouseCapabilities;
using Domain.WarehouseCapabilityOperations;
using Domain.WarehouseDocuments;
using Domain.WarehouseMaterialSettings;
using Domain.Warehouses;
using Infrastructure.Storage;
using SharedKernel;

namespace Infrastructure.AuditLogs;

/// <summary>
/// Maps every tracked business entity type to its canonical audit identity: the stable
/// <see cref="KnownAuditEntityTypes"/> name, the owning aggregate (when the entity is a detail of
/// one), and pure scalar selectors for the audited entity/aggregate ids. Types that must never be
/// field-captured are classified here as technical exclusions with a written reason so the
/// registry-completeness test can fail the build when a new entity is unclassified.
/// </summary>
internal sealed class AuditEntityRegistry
{
    public sealed record Mapping(
        string EntityType,
        string? AggregateType,
        Func<object, Guid?> EntityIdSelector,
        Func<object, Guid?>? AggregateIdSelector);

    private static readonly Dictionary<Type, Mapping> Mappings = CreateMappings();

    public static IReadOnlyDictionary<Type, string> TechnicalExclusions { get; } =
        new Dictionary<Type, string>
        {
            [typeof(AuditLog)] =
                "Audit sink itself; excluded unconditionally to prevent recursive capture.",
            [typeof(AuditLogEntry)] =
                "Audit sink itself; excluded unconditionally to prevent recursive capture.",
            [typeof(DocumentLifecycleEvent)] =
                "Dedicated immutable document lifecycle ledger; field-audit capture would duplicate the same evidence.",
            [typeof(RefreshToken)] =
                "Holds bearer token secrets; login/refresh are captured as synthetic User-root Authenticate/TokenRefresh headers instead.",
            [typeof(PendingFileDeletion)] =
                "Operational infrastructure queue backing attachment cleanup; not business data.",
            [typeof(DocumentSequence)] =
                "Internal numbering state maintained by raw-SQL upsert; the resulting document reference is audited instead.",
            [typeof(AssetCurrentStatusView)] =
                "Read-only database view; never mutated at runtime.",
            [typeof(Permission)] =
                "Compiled-in reference data seeded by migration; not user-manageable at runtime."
        };

    public bool TryGet(Type clrType, out Mapping mapping) =>
        Mappings.TryGetValue(clrType, out mapping!);

    private static Dictionary<Type, Mapping> CreateMappings() => new()
    {
        [typeof(User)] = Self<User>(),
        [typeof(Role)] = Self<Role>(),
        [typeof(UserRoleScope)] = new(
            "UserRoleScope",
            "Role",
            e => ((UserRoleScope)e).Id,
            e => ((UserRoleScope)e).RoleId),
        [typeof(Organization)] = Self<Organization>(),
        [typeof(Site)] = Self<Site>(),
        [typeof(OrganizationalUnit)] = Self<OrganizationalUnit>(),
        [typeof(Employee)] = Self<Employee>(),
        [typeof(ExternalParty)] = Self<ExternalParty>(),
        [typeof(UnitOfMeasure)] = Self<UnitOfMeasure>(),
        [typeof(MaterialDomain)] = Self<MaterialDomain>(),
        [typeof(MaterialCategory)] = Self<MaterialCategory>(),
        [typeof(MaterialFamily)] = Self<MaterialFamily>(),
        [typeof(Material)] = Self<Material>(),
        [typeof(MaterialUnitConversion)] = Self<MaterialUnitConversion>(),
        [typeof(Warehouse)] = Self<Warehouse>(),
        [typeof(WarehouseCapability)] = Self<WarehouseCapability>(),
        [typeof(WarehouseCapabilityOperation)] = Self<WarehouseCapabilityOperation>(),
        [typeof(WarehouseMaterialSetting)] = Self<WarehouseMaterialSetting>(),
        [typeof(WarehouseDocument)] = Self<WarehouseDocument>(),
        [typeof(InventoryBalance)] = Self<InventoryBalance>(),
        [typeof(Asset)] = Self<Asset>(),
        [typeof(Custody)] = Self<Custody>(),
        [typeof(TrackedMaterialUnit)] = Self<TrackedMaterialUnit>(),
        [typeof(DurableCustodyAllocation)] = Self<DurableCustodyAllocation>(),
        [typeof(DurableCustodyHistory)] = new(
            "DurableCustodyHistory",
            "DurableCustody",
            e => ((DurableCustodyHistory)e).Id,
            e => ((DurableCustodyHistory)e).SubjectId),
        [typeof(InventoryCount)] = Self<InventoryCount>(),
        [typeof(InventoryAdjustment)] = Self<InventoryAdjustment>(),

        // Document details trace back to the WarehouseDocument aggregate.
        [typeof(DocumentLine)] = Detail<DocumentLine>(
            e => ((DocumentLine)e).DocumentId),
        [typeof(DocumentAttachment)] = Detail<DocumentAttachment>(
            e => ((DocumentAttachment)e).DocumentId),
        [typeof(StockMovement)] = Detail<StockMovement>(
            e => ((StockMovement)e).DocumentId),
        [typeof(DocumentLineAssetSelection)] = Detail<DocumentLineAssetSelection>(
            e => ((DocumentLineAssetSelection)e).DocumentId),
        [typeof(AdjustmentLine)] = Detail<AdjustmentLine>(
            e => ((AdjustmentLine)e).AdjustmentId),

        // 1:1 petals share the primary key with their WarehouseDocument.
        [typeof(ReceivingInfo)] = DocumentPetal<ReceivingInfo>(),
        [typeof(IssueTo)] = DocumentPetal<IssueTo>(),
        [typeof(TransferInfo)] = DocumentPetal<TransferInfo>(),
        [typeof(ReturnInfo)] = DocumentPetal<ReturnInfo>(),

        // Count details trace back to the InventoryCount aggregate.
        [typeof(InventoryCountLine)] = new(
            "InventoryCountLine",
            "InventoryCount",
            e => ((InventoryCountLine)e).Id,
            e => ((InventoryCountLine)e).CountId),
        [typeof(InventoryCountScopeMaterial)] = new(
            "InventoryCountScopeMaterial",
            "InventoryCount",
            e => ((InventoryCountScopeMaterial)e).Id,
            e => ((InventoryCountScopeMaterial)e).CountId),

        [typeof(CustodyHistory)] = new(
            "CustodyHistory",
            "Custody",
            e => ((CustodyHistory)e).Id,
            e => ((CustodyHistory)e).CustodyId),

        [typeof(AssetMovementHistory)] = new(
            "AssetMovementHistory",
            "Asset",
            e => ((AssetMovementHistory)e).Id,
            e => ((AssetMovementHistory)e).AssetId),

        // Composite-key adapter: permission_id stays a captured field, RoleId identifies both.
        [typeof(RolePermission)] = new(
            "RolePermission",
            "Role",
            e => ((RolePermission)e).RoleId,
            e => ((RolePermission)e).RoleId),

        [typeof(RoleAllowedScopeType)] = new(
            "RoleAllowedScopeType",
            "Role",
            e => ((RoleAllowedScopeType)e).RoleId,
            e => ((RoleAllowedScopeType)e).RoleId)
    };

    private static Mapping Self<TEntity>() where TEntity : Entity =>
        new(typeof(TEntity).Name, null, e => ((TEntity)e).Id, null);

    private static Mapping Detail<TEntity>(Func<object, Guid?> aggregateIdSelector)
        where TEntity : Entity =>
        new(typeof(TEntity).Name, "WarehouseDocument", e => ((TEntity)e).Id, aggregateIdSelector);

    private static Mapping DocumentPetal<TEntity>() where TEntity : Entity =>
        new(typeof(TEntity).Name, "WarehouseDocument", e => ((TEntity)e).Id, e => ((TEntity)e).Id);
}
