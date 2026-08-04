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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
