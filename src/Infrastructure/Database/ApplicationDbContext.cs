using Application.Abstractions.Data;
using Domain.DocumentAttachments;
using Domain.Assets;
using Domain.AssetMovementHistories;
using Domain.Custodies;
using Domain.CustodyHistories;
using Domain.DocumentLineAssetSelections;
using Domain.DocumentLines;
using Domain.DocumentSequences;
using Domain.Employees;
using Domain.InventoryBalances;
using Domain.InventoryAdjustments;
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
using Domain.Roles;
using Domain.ReceivingInfos;
using Domain.ReturnInfos;
using Domain.Sites;
using Domain.StockMovements;
using Domain.TransferInfos;
using Domain.UnitsOfMeasure;
using Domain.Users;
using Domain.UserRoleScopes;
using Domain.WarehouseCapabilities;
using Domain.WarehouseCapabilityOperations;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Domain.WarehouseMaterialSettings;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.Database;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IDomainEventsDispatcher domainEventsDispatcher)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users { get; set; }

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public DbSet<Organization> Organizations { get; set; }

    public DbSet<Site> Sites { get; set; }

    public DbSet<OrganizationalUnit> OrganizationalUnits { get; set; }

    public DbSet<Employee> Employees { get; set; }

    public DbSet<Role> Roles { get; set; }

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

    public DbSet<DocumentLineAssetSelection> DocumentLineAssetSelections { get; set; }

    public DbSet<ReturnInfo> ReturnInfos { get; set; }

    public DbSet<AssetCurrentStatusView> AssetCurrentStatuses { get; set; }

    public DbSet<InventoryCount> InventoryCounts { get; set; }

    public DbSet<InventoryCountScopeMaterial> InventoryCountScopeMaterials { get; set; }

    public DbSet<InventoryCountLine> InventoryCountLines { get; set; }

    public DbSet<InventoryAdjustment> InventoryAdjustments { get; set; }

    public DbSet<AdjustmentLine> AdjustmentLines { get; set; }

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

        List<IDomainEvent> domainEvents = ExtractDomainEvents();
        int result = await base.SaveChangesAsync(cancellationToken);

        await PublishDomainEventsAsync(domainEvents);

        return result;
    }

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
