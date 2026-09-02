using Application.Abstractions.Data;
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
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Infrastructure.Database;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IDomainEventsDispatcher domainEventsDispatcher,
    HybridCache? hybridCache = null)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users { get; set; }

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public DbSet<Organization> Organizations { get; set; }

    public DbSet<Site> Sites { get; set; }

    public DbSet<OrganizationalUnit> OrganizationalUnits { get; set; }

    public DbSet<Employee> Employees { get; set; }

    public DbSet<ExternalParty> ExternalParties { get; set; }

    public DbSet<Role> Roles { get; set; }

    public DbSet<RoleAllowedScopeType> RoleAllowedScopeTypes { get; set; }

    public DbSet<Permission> Permissions { get; set; }

    public DbSet<RolePermission> RolePermissions { get; set; }

    public DbSet<UserRoleScope> UserRoleScopes { get; set; }

    public DbSet<UnitOfMeasure> UnitsOfMeasure { get; set; }

    public DbSet<MaterialDomain> MaterialDomains { get; set; }

    public DbSet<MaterialCategory> MaterialCategories { get; set; }

    public DbSet<MaterialFamily> MaterialFamilies { get; set; }

    public DbSet<Material> Materials { get; set; }

    public DbSet<MaterialUnitConversion> MaterialUnitConversions { get; set; }

    public DbSet<Warehouse> Warehouses { get; set; }

    public DbSet<WarehouseCapability> WarehouseCapabilities { get; set; }

    public DbSet<WarehouseCapabilityOperation> WarehouseCapabilityOperations { get; set; }

    public DbSet<WarehouseMaterialSetting> WarehouseMaterialSettings { get; set; }

    public DbSet<DocumentSequence> DocumentSequences { get; set; }

    public DbSet<WarehouseDocument> WarehouseDocuments { get; set; }

    public DbSet<DocumentLine> DocumentLines { get; set; }

    public DbSet<DocumentLifecycleEvent> DocumentLifecycleEvents { get; set; }

    public DbSet<DocumentAttachment> DocumentAttachments { get; set; }

    public DbSet<StockMovement> StockMovements { get; set; }

    public DbSet<InventoryBalance> InventoryBalances { get; set; }

    public DbSet<Asset> Assets { get; set; }

    public DbSet<ReceivingInfo> ReceivingInfos { get; set; }

    public DbSet<IssueTo> IssueTos { get; set; }

    public DbSet<TransferInfo> TransferInfos { get; set; }

    public DbSet<AssetMovementHistory> AssetMovementHistories { get; set; }

    public DbSet<Custody> Custodies { get; set; }

    public DbSet<CustodyHistory> CustodyHistories { get; set; }

    public DbSet<TrackedMaterialUnit> TrackedMaterialUnits { get; set; }

    public DbSet<DurableCustodyAllocation> DurableCustodyAllocations { get; set; }

    public DbSet<DurableCustodyHistory> DurableCustodyHistories { get; set; }

    public DbSet<DocumentLineAssetSelection> DocumentLineAssetSelections { get; set; }

    public DbSet<ReturnInfo> ReturnInfos { get; set; }

    public DbSet<AssetCurrentStatusView> AssetCurrentStatuses { get; set; }

    public DbSet<InventoryCount> InventoryCounts { get; set; }

    public DbSet<InventoryCountScopeMaterial> InventoryCountScopeMaterials { get; set; }

    public DbSet<InventoryCountLine> InventoryCountLines { get; set; }

    public DbSet<InventoryAdjustment> InventoryAdjustments { get; set; }

    public DbSet<AdjustmentLine> AdjustmentLines { get; set; }

    public DbSet<AuditLog> AuditLogs { get; set; }

    public DbSet<AuditLogEntry> AuditLogEntries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        modelBuilder.HasDefaultSchema(Schemas.Default);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // When should you publish domain events?
        //
        // 1. BEFORE calling SaveChangesAsync
        //     - domain events are part of the same transaction
        //     - immediate consistency
        // 2. AFTER calling SaveChangesAsync
        //     - domain events are a separate transaction
        //     - eventual consistency
        //     - handlers can fail

        string[] invalidatedCacheTags = GetInvalidatedCacheTags();
        List<IDomainEvent> domainEvents = ExtractDomainEvents();
        int result = await base.SaveChangesAsync(cancellationToken);

        if (hybridCache is not null)
        {
            await Task.WhenAll(invalidatedCacheTags.Select(tag =>
                hybridCache.RemoveByTagAsync(tag, cancellationToken).AsTask()));
        }

        await PublishDomainEventsAsync(domainEvents);

        return result;
    }

    private string[] GetInvalidatedCacheTags() => ChangeTracker.Entries()
        .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
        .Select(entry => entry.Entity switch
        {
            Organization => "organizations",
            Site => "sites",
            OrganizationalUnit => "organizational-units",
            Employee => "employees",
            ExternalParty => "external-parties",
            MaterialDomain => "domains",
            MaterialCategory => "categories",
            MaterialFamily => "families",
            Material => "materials",
            UnitOfMeasure => "uom",
            Warehouse => "warehouses",
            User or Role or RolePermission or UserRoleScope => "auth-roles",
            _ => null
        })
        .Where(tag => tag is not null)
        .Select(tag => tag!)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private async Task PublishDomainEventsAsync(IEnumerable<IDomainEvent> domainEvents)
    {
        await domainEventsDispatcher.DispatchAsync(domainEvents);
    }

    private List<IDomainEvent> ExtractDomainEvents()
    {
        var domainEvents = ChangeTracker
            .Entries()
            .Select(entry => entry.Entity)
            .OfType<IDomainEventSource>()
            .SelectMany(source =>
            {
                List<IDomainEvent> domainEvents = source.DomainEvents;

                source.ClearDomainEvents();

                return domainEvents;
            })
            .ToList();
        return domainEvents;
    }
}
