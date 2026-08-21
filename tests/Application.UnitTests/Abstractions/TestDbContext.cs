using Application.Abstractions.Data;
using Domain.Assets;
using Domain.AssetMovementHistories;
using Domain.Custodies;
using Domain.CustodyHistories;
using Domain.DocumentLineAssetSelections;
using Domain.DocumentAttachments;
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
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Abstractions;

/// <summary>
/// A lightweight in-memory <see cref="DbContext"/> that implements <see cref="IApplicationDbContext"/>
/// so Application handlers can be unit tested without referencing the Infrastructure layer.
/// </summary>
public sealed class TestDbContext(DbContextOptions<TestDbContext> options)
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
        modelBuilder.Entity<RolePermission>()
            .HasKey(rolePermission => new { rolePermission.RoleId, rolePermission.PermissionId });

        modelBuilder.Entity<User>()
            .HasIndex(user => user.EmployeeId)
            .IsUnique();

        modelBuilder.Entity<UserRoleScope>()
            .HasIndex(scope => new { scope.UserId, scope.RoleId, scope.ScopeType, scope.ScopeId })
            .IsUnique();

        modelBuilder.Entity<MaterialUnitConversion>()
            .HasIndex(conversion => new { conversion.MaterialId, conversion.FromUnitId })
            .IsUnique();

        modelBuilder.Entity<Warehouse>()
            .HasIndex(warehouse => warehouse.Code)
            .IsUnique();

        modelBuilder.Entity<WarehouseCapability>()
            .HasIndex(capability => new { capability.WarehouseId, capability.MaterialDomainId })
            .IsUnique();

        modelBuilder.Entity<WarehouseCapabilityOperation>()
            .HasIndex(operation => new { operation.CapabilityId, operation.OperationType })
            .IsUnique();

        modelBuilder.Entity<WarehouseMaterialSetting>()
            .HasIndex(setting => new { setting.WarehouseId, setting.MaterialId })
            .IsUnique();

        modelBuilder.Entity<DocumentSequence>()
            .HasIndex(sequence => new { sequence.SiteId, sequence.DocumentType, sequence.Year })
            .IsUnique();

        modelBuilder.Entity<WarehouseDocument>()
            .HasIndex(document => document.SystemReferenceNumber)
            .IsUnique();

        modelBuilder.Entity<StockMovement>()
            .HasIndex(movement => new { movement.DocumentId, movement.LineId, movement.MovementType })
            .IsUnique();

        modelBuilder.Entity<InventoryBalance>()
            .HasIndex(balance => new { balance.WarehouseId, balance.MaterialId })
            .IsUnique();

        modelBuilder.Entity<Asset>()
            .HasIndex(asset => asset.AssetNumber)
            .IsUnique();

        modelBuilder.Entity<ReceivingInfo>()
            .HasKey(info => info.Id);

        modelBuilder.Entity<IssueTo>()
            .HasKey(issueTo => issueTo.Id);

        modelBuilder.Entity<TransferInfo>()
            .HasKey(transferInfo => transferInfo.Id);

        modelBuilder.Entity<AssetMovementHistory>()
            .HasKey(history => history.Id);

        modelBuilder.Entity<Custody>()
            .HasKey(custody => custody.Id);

        modelBuilder.Entity<CustodyHistory>()
            .HasKey(history => history.Id);

        modelBuilder.Entity<DocumentLineAssetSelection>()
            .HasKey(selection => selection.Id);

        modelBuilder.Entity<ReturnInfo>()
            .HasKey(info => info.Id);

        modelBuilder.Entity<AssetCurrentStatusView>()
            .HasKey(status => status.AssetId);

        modelBuilder.Entity<InventoryCount>().HasKey(count => count.Id);
        modelBuilder.Entity<InventoryCountScopeMaterial>().HasKey(material => material.Id);
        modelBuilder.Entity<InventoryCountLine>().HasKey(line => line.Id);
        modelBuilder.Entity<InventoryAdjustment>().HasKey(adjustment => adjustment.Id);
        modelBuilder.Entity<AdjustmentLine>().HasKey(line => line.Id);
    }
}
