using Domain.Materials;
using Domain.Warehouses;
using Domain.WarehouseMaterialSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.WarehouseMaterialSettings;

internal sealed class WarehouseMaterialSettingConfiguration : IEntityTypeConfiguration<WarehouseMaterialSetting>
{
    public void Configure(EntityTypeBuilder<WarehouseMaterialSetting> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasIndex(s => new { s.WarehouseId, s.MaterialId }).IsUnique();

        builder.Property(s => s.MinQuantity).HasPrecision(18, 3);

        builder.Property(s => s.MaxQuantity).HasPrecision(18, 3);

        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);

        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("ck_warehouse_material_settings_min_non_negative", "min_quantity >= 0");
            tableBuilder.HasCheckConstraint("ck_warehouse_material_settings_max_non_negative", "max_quantity >= 0");
            tableBuilder.HasCheckConstraint("ck_warehouse_material_settings_min_le_max", "min_quantity <= max_quantity");
            tableBuilder.HasCheckConstraint(
                "ck_warehouse_material_settings_status_valid",
                "status IN ('Active', 'Inactive')");
        });

        builder.HasOne<Warehouse>().WithMany().HasForeignKey(s => s.WarehouseId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Material>().WithMany().HasForeignKey(s => s.MaterialId).OnDelete(DeleteBehavior.Restrict);
    }
}
