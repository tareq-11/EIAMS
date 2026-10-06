using Domain.Employees;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Users;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.HasIndex(u => u.Email).IsUnique();

        builder.HasIndex(u => u.Username).IsUnique();

        builder.HasIndex(u => u.EmployeeId).IsUnique();

        builder.Property(u => u.Email).HasMaxLength(256);

        builder.Property(u => u.Username).HasMaxLength(100);

        builder.Property(u => u.FirstName).HasMaxLength(200);

        builder.Property(u => u.LastName).HasMaxLength(200);

        builder.Property(u => u.PasswordHash).HasMaxLength(500);

        builder.Property(u => u.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasSentinel((UserStatus)0)
            .HasDefaultValue(UserStatus.Active);

        builder.HasIndex(u => new { u.Status, u.Email });

        // Concurrent writers are reported instead of silently overwriting each other.
        // The application handlers additionally compare the client's expected version
        // and return 409 before the save, so the token is belt and braces.
        builder.Property(u => u.RowVersion).IsConcurrencyToken();

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "ck_users_row_version_positive",
                "row_version > 0"));

        builder.HasOne<Employee>().WithMany().HasForeignKey(u => u.EmployeeId);
    }
}