using Domain.DocumentAttachments;
using Domain.Assets;
using Domain.DocumentLines;
using Domain.DocumentSequences;
using Domain.Employees;
using Domain.InventoryBalances;
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

namespace Application.Abstractions.Data;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Organization> Organizations { get; }
    DbSet<Site> Sites { get; }
    DbSet<OrganizationalUnit> OrganizationalUnits { get; }
    DbSet<Employee> Employees { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserRoleScope> UserRoleScopes { get; }
    DbSet<UnitOfMeasure> UnitsOfMeasure { get; }
    DbSet<MaterialDomain> MaterialDomains { get; }
    DbSet<MaterialCategory> MaterialCategories { get; }
    DbSet<MaterialFamily> MaterialFamilies { get; }
    DbSet<Material> Materials { get; }
    DbSet<MaterialUnitConversion> MaterialUnitConversions { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<WarehouseCapability> WarehouseCapabilities { get; }
    DbSet<WarehouseCapabilityOperation> WarehouseCapabilityOperations { get; }
    DbSet<WarehouseMaterialSetting> WarehouseMaterialSettings { get; }
    DbSet<DocumentSequence> DocumentSequences { get; }
    DbSet<WarehouseDocument> WarehouseDocuments { get; }
    DbSet<DocumentLine> DocumentLines { get; }
    DbSet<DocumentAttachment> DocumentAttachments { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<InventoryBalance> InventoryBalances { get; }
    DbSet<Asset> Assets { get; }
    DbSet<ReceivingInfo> ReceivingInfos { get; }
    DbSet<IssueTo> IssueTos { get; }
    DbSet<TransferInfo> TransferInfos { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
