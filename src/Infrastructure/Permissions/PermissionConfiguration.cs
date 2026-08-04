using Application.Abstractions.Authorization;
using Domain.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Permissions;

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.Code).IsUnique();

        builder.Property(p => p.Code).HasMaxLength(100);

        builder.HasData(
            new
            {
                Id = WellKnownPermissions.UsersAccessId,
                Code = "users:access",
                Description = (string?)"Access another user's profile."
            },
            new
            {
                Id = WellKnownPermissions.OrganizationsManageId,
                Code = PermissionCodes.Organizations.Manage,
                Description = (string?)"Create, update, and change the status of organizations."
            },
            new
            {
                Id = WellKnownPermissions.SitesManageId,
                Code = PermissionCodes.Sites.Manage,
                Description = (string?)"Create, update, and change the status of sites."
            },
            new
            {
                Id = WellKnownPermissions.OrganizationalUnitsManageId,
                Code = PermissionCodes.OrganizationalUnits.Manage,
                Description = (string?)"Create, update, and change the status of organizational units."
            },
            new
            {
                Id = WellKnownPermissions.EmployeesManageId,
                Code = PermissionCodes.Employees.Manage,
                Description = (string?)"Create, update, and change the status of employees."
            },
            new
            {
                Id = WellKnownPermissions.RolesManageId,
                Code = PermissionCodes.Roles.Manage,
                Description = (string?)"Manage roles, role permissions, and user role scope grants."
            },
            new
            {
                Id = WellKnownPermissions.UnitsOfMeasureManageId,
                Code = PermissionCodes.UnitsOfMeasure.Manage,
                Description = (string?)"Create and update units of measure."
            },
            new
            {
                Id = WellKnownPermissions.MaterialDomainsManageId,
                Code = PermissionCodes.MaterialDomains.Manage,
                Description = (string?)"Create, update, and change the status of material domains."
            },
            new
            {
                Id = WellKnownPermissions.MaterialCategoriesManageId,
                Code = PermissionCodes.MaterialCategories.Manage,
                Description = (string?)"Create, update, and change the status of material categories."
            },
            new
            {
                Id = WellKnownPermissions.MaterialFamiliesManageId,
                Code = PermissionCodes.MaterialFamilies.Manage,
                Description = (string?)"Create, update, and change the status of material families."
            },
            new
            {
                Id = WellKnownPermissions.MaterialsManageId,
                Code = PermissionCodes.Materials.Manage,
                Description = (string?)"Create, update, and change the status of materials and their unit conversions."
            });
    }
}
