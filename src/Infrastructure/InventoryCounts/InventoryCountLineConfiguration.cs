using Domain.Assets;
using Domain.InventoryCounts;
using Domain.Materials;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.InventoryCounts;

internal sealed class InventoryCountLineConfiguration : IEntityTypeConfiguration<InventoryCountLine>
{
    public void Configure(EntityTypeBuilder<InventoryCountLine> builder)
    {
        builder.ToTable("inventory_count_lines", table =>
        {
            table.HasCheckConstraint("ck_inventory_count_lines_snapshot_nonnegative", "snapshot_quantity >= 0");
            table.HasCheckConstraint("ck_inventory_count_lines_actual_nonnegative", "actual_quantity IS NULL OR actual_quantity >= 0");
            table.HasCheckConstraint("ck_inventory_count_lines_actual_difference", "(actual_quantity IS NULL AND difference IS NULL) OR (actual_quantity IS NOT NULL AND difference = actual_quantity - snapshot_quantity)");
            table.HasCheckConstraint("ck_inventory_count_lines_asset_quantities", "asset_id IS NULL OR (snapshot_quantity IN (0, 1) AND (actual_quantity IS NULL OR actual_quantity IN (0, 1)))");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.SnapshotQuantity).HasPrecision(18, 3);
        builder.Property(item => item.ActualQuantity).HasPrecision(18, 3);
        builder.Property(item => item.Difference).HasPrecision(18, 3);
        builder.Property(item => item.VarianceReason).HasMaxLength(200);
        builder.HasIndex(item => new { item.CountId, item.MaterialId }).HasFilter("asset_id IS NULL").IsUnique();
        builder.HasIndex(item => new { item.CountId, item.AssetId }).HasFilter("asset_id IS NOT NULL").IsUnique();
        builder.HasOne<InventoryCount>().WithMany().HasForeignKey(item => item.CountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Material>().WithMany().HasForeignKey(item => item.MaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Asset>().WithMany().HasForeignKey(item => item.AssetId).OnDelete(DeleteBehavior.Restrict);
    }
}
