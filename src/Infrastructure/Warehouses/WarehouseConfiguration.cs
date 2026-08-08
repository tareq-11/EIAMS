using Domain.Sites;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Warehouses;

internal sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.HasKey(w => w.Id);

        builder.HasIndex(w => w.Code).IsUnique();

        builder.Property(w => w.Name).HasMaxLength(200);

        builder.Property(w => w.Code).HasMaxLength(50);

        builder.Property(w => w.WarehouseType).HasMaxLength(50);

        builder.Property(w => w.Status).HasConversion<string>().HasMaxLength(20);

        builder.Property(w => w.RowVersion).IsConcurrencyToken();

        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("ck_warehouses_row_version_positive", "row_version > 0");
            tableBuilder.HasCheckConstraint("ck_warehouses_status_valid", "status IN ('Active', 'Inactive')");
        });

        builder.HasOne<Site>().WithMany().HasForeignKey(w => w.SiteId).OnDelete(DeleteBehavior.Restrict);
    }
}
