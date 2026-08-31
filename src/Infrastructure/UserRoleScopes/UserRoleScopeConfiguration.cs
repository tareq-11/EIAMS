using Domain.Roles;
using Domain.UserRoleScopes;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.UserRoleScopes;

internal sealed class UserRoleScopeConfiguration : IEntityTypeConfiguration<UserRoleScope>
{
    public void Configure(EntityTypeBuilder<UserRoleScope> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ScopeType).HasConversion<string>().HasMaxLength(30);

        builder.HasIndex(s => s.UserId)
            .HasDatabaseName("ux_user_role_scopes_user_id")
            .IsUnique();

        builder.ToTable(tableBuilder => tableBuilder.HasCheckConstraint(
            "ck_user_role_scopes_scope_id",
            "(scope_type = 'Enterprise' AND scope_id IS NULL) OR " +
            "(scope_type IN ('Site', 'OrganizationalUnit', 'Warehouse') AND scope_id IS NOT NULL)"));

        builder.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Role>().WithMany().HasForeignKey(s => s.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}
