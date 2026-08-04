using Domain.Employees;
using Domain.OrganizationalUnits;
using Domain.Organizations;
using Domain.Permissions;
using Domain.Roles;
using Domain.Sites;
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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
