using Domain.InventoryAdjustments;
using Domain.InventoryCounts;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.InventoryAdjustments;

internal sealed class InventoryAdjustmentConfiguration : IEntityTypeConfiguration<InventoryAdjustment>
{
    public void Configure(EntityTypeBuilder<InventoryAdjustment> builder)
    {
        builder.ToTable("inventory_adjustments", table =>
        {
            table.HasCheckConstraint("ck_inventory_adjustments_kind_valid", "adjustment_kind IN ('Quantity', 'Disposal')");
            table.HasCheckConstraint("ck_inventory_adjustments_status_valid", "status IN ('Draft', 'Posted', 'Reversed')");
            table.HasCheckConstraint("ck_inventory_adjustments_disposal_terminal", "NOT (adjustment_kind = 'Disposal' AND status = 'Reversed')");
            table.HasCheckConstraint("ck_inventory_adjustments_reason_not_blank", "length(btrim(reason)) > 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.AdjustmentKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Reason).HasMaxLength(500).IsRequired();
        builder.HasIndex(item => item.CountId).HasFilter("count_id IS NOT NULL").IsUnique();
        builder.HasOne<WarehouseDocument>().WithOne().HasForeignKey<InventoryAdjustment>(item => item.Id).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InventoryCount>().WithMany().HasForeignKey(item => item.CountId).OnDelete(DeleteBehavior.Restrict);
    }
}
