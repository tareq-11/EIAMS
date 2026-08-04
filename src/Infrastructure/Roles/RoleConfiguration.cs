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

        builder.HasData(
            new
            {
                Id = WellKnownRoles.AdministratorId,
                Name = "Administrator",
                Description = "Full enterprise administrative access. Automatically granted to the first registered user.",
                CreatedAtUtc = SeedConstants.SeedTimestampUtc,
                UpdatedAtUtc = (DateTime?)null,
                CreatedBy = (Guid?)null,
                UpdatedBy = (Guid?)null
            },
            new
            {
                Id = WellKnownRoles.WarehouseKeeperId,
                Name = "WH_KEEPER",
                Description = "Creates and submits warehouse documents (D-WF-01). Permissions reserved for M3+.",
                CreatedAtUtc = SeedConstants.SeedTimestampUtc,
                UpdatedAtUtc = (DateTime?)null,
                CreatedBy = (Guid?)null,
                UpdatedBy = (Guid?)null
            },
            new
            {
                Id = WellKnownRoles.WarehouseManagerId,
                Name = "WH_MGR",
                Description = "Posts and reverses warehouse documents (D-WF-01). Permissions reserved for M3+.",
                CreatedAtUtc = SeedConstants.SeedTimestampUtc,
                UpdatedAtUtc = (DateTime?)null,
                CreatedBy = (Guid?)null,
                UpdatedBy = (Guid?)null
            });
    }
}
