using Domain.Roles;
using Domain.Users;
using Domain.UserRoleScopes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.UserRoleScopes;

internal sealed class UserRoleScopeConfiguration : IEntityTypeConfiguration<UserRoleScope>
{
    public void Configure(EntityTypeBuilder<UserRoleScope> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ScopeType).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(s => new { s.UserId, s.RoleId, s.ScopeType })
            .HasDatabaseName("ux_user_role_scopes_enterprise")
            .IsUnique()
            .HasFilter("scope_id IS NULL");

        builder.HasIndex(s => new { s.UserId, s.RoleId, s.ScopeType, s.ScopeId })
            .HasDatabaseName("ux_user_role_scopes_scoped")
            .IsUnique()
            .HasFilter("scope_id IS NOT NULL");

        builder.ToTable(tableBuilder => tableBuilder.HasCheckConstraint(
            "ck_user_role_scopes_scope_id",
            "(scope_type = 'Enterprise' AND scope_id IS NULL) OR " +
            "(scope_type IN ('Site', 'Warehouse') AND scope_id IS NOT NULL)"));

        builder.HasOne<User>().WithMany().HasForeignKey(s => s.UserId);

        builder.HasOne<Role>().WithMany().HasForeignKey(s => s.RoleId);
    }
}
