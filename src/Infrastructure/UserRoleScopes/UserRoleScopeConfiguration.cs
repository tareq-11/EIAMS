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

        builder.HasIndex(s => new { s.UserId, s.RoleId, s.ScopeType, s.ScopeId });

        builder.HasOne<User>().WithMany().HasForeignKey(s => s.UserId);

        builder.HasOne<Role>().WithMany().HasForeignKey(s => s.RoleId);
    }
}
