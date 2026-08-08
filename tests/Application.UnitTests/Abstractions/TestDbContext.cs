using Application.Abstractions.Data;
using Domain.DocumentSequences;
using Domain.Employees;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.MaterialUnitConversions;
using Domain.OrganizationalUnits;
using Domain.Organizations;
using Domain.Permissions;
using Domain.Roles;
using Domain.Sites;
using Domain.UnitsOfMeasure;
using Domain.Users;
using Domain.UserRoleScopes;
using Domain.WarehouseCapabilities;
using Domain.WarehouseCapabilityOperations;
using Domain.Warehouses;
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
    }
}
