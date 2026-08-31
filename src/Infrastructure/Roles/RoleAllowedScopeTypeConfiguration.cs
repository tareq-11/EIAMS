using Domain.Common;
using Domain.Roles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Roles;

internal sealed class RoleAllowedScopeTypeConfiguration : IEntityTypeConfiguration<RoleAllowedScopeType>
{
    public void Configure(EntityTypeBuilder<RoleAllowedScopeType> builder)
    {
        builder.HasKey(item => new { item.RoleId, item.ScopeType });

        builder.Property(item => item.ScopeType)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(item => item.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasData(
            new { RoleId = WellKnownRoles.AdministratorId, ScopeType = ScopeType.Enterprise },
            new { RoleId = WellKnownRoles.WarehouseKeeperId, ScopeType = ScopeType.Warehouse },
            new { RoleId = WellKnownRoles.WarehouseManagerId, ScopeType = ScopeType.Warehouse },
            new { RoleId = WellKnownRoles.WarehouseManagerId, ScopeType = ScopeType.OrganizationalUnit });
    }
}
