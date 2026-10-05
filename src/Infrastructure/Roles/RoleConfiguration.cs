using Domain.Roles;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Roles;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(r => r.Id);

        builder.HasIndex(r => r.Name).IsUnique();

        builder.Property(r => r.Name).HasMaxLength(100);

        builder.Property(r => r.NameAr).IsRequired().HasMaxLength(Role.NameArMaxLength);

        builder.Property(r => r.RowVersion).IsConcurrencyToken();

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_roles_name_ar_not_blank",
                "length(btrim(name_ar)) > 0");
            table.HasCheckConstraint(
                "ck_roles_row_version_positive",
                "row_version > 0");
        });

        builder.HasData(
            new
            {
                Id = WellKnownRoles.AdministratorId,
                Name = "SYSTEM_ADMIN",
                NameAr = "مدير النظام",
                Description = "Enterprise structural administration. Created only by explicit startup bootstrap configuration.",
                RowVersion = 1,
                CreatedAtUtc = SeedConstants.SeedTimestampUtc,
                UpdatedAtUtc = (DateTime?)null,
                CreatedBy = (Guid?)null,
                UpdatedBy = (Guid?)null
            },
            new
            {
                Id = WellKnownRoles.WarehouseKeeperId,
                Name = "WH_KEEPER",
                NameAr = "أمين المستودع",
                Description = "Creates and submits warehouse documents (D-WF-01). Permissions reserved for M3+.",
                RowVersion = 1,
                CreatedAtUtc = SeedConstants.SeedTimestampUtc,
                UpdatedAtUtc = (DateTime?)null,
                CreatedBy = (Guid?)null,
                UpdatedBy = (Guid?)null
            },
            new
            {
                Id = WellKnownRoles.WarehouseManagerId,
                Name = "WH_MGR",
                NameAr = "مدير المستودع",
                Description = "Posts and reverses warehouse documents (D-WF-01). Permissions reserved for M3+.",
                RowVersion = 1,
                CreatedAtUtc = SeedConstants.SeedTimestampUtc,
                UpdatedAtUtc = (DateTime?)null,
                CreatedBy = (Guid?)null,
                UpdatedBy = (Guid?)null
            },
            new
            {
                Id = WellKnownRoles.AuditorId,
                Name = "AUDITOR",
                NameAr = "مدقق",
                Description = "Read-only audit and operational reporting role.",
                RowVersion = 1,
                CreatedAtUtc = SeedConstants.SeedTimestampUtc,
                UpdatedAtUtc = (DateTime?)null,
                CreatedBy = (Guid?)null,
                UpdatedBy = (Guid?)null
            });
    }
}
