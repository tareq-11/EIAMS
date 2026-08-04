using Domain.Permissions;
using Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Roles;

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.HasKey(rp => new { rp.RoleId, rp.PermissionId });

        builder.HasOne<Role>().WithMany().HasForeignKey(rp => rp.RoleId);

        builder.HasOne<Permission>().WithMany().HasForeignKey(rp => rp.PermissionId);

        // The Administrator role gets every permission that exists today; WH_KEEPER/WH_MGR are
        // seeded with none - their grants arrive with the document workflow they gate (M3+).
        builder.HasData(
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.UsersAccessId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.OrganizationsManageId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.SitesManageId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.OrganizationalUnitsManageId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.EmployeesManageId },
            new { RoleId = WellKnownRoles.AdministratorId, PermissionId = WellKnownPermissions.RolesManageId });
    }
}
