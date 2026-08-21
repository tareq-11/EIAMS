using Domain.InventoryCounts;
using Domain.MaterialDomains;
using Domain.Users;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.InventoryCounts;

internal sealed class InventoryCountConfiguration : IEntityTypeConfiguration<InventoryCount>
{
    public void Configure(EntityTypeBuilder<InventoryCount> builder)
    {
        builder.ToTable("inventory_counts", table =>
        {
            table.HasCheckConstraint("ck_inventory_counts_type_valid", "count_type IN ('Scheduled', 'Surprise', 'Cycle')");
            table.HasCheckConstraint("ck_inventory_counts_scope_valid", "scope_type IN ('EntireWarehouse', 'MaterialDomain', 'SelectedMaterials')");
            table.HasCheckConstraint("ck_inventory_counts_freeze_valid", "freeze_policy IN ('HardFreeze', 'SoftFreeze', 'NoFreeze')");
            table.HasCheckConstraint("ck_inventory_counts_status_valid", "status IN ('Planned', 'InProgress', 'Completed', 'Closed')");
            table.HasCheckConstraint("ck_inventory_counts_row_version_positive", "row_version > 0");
            table.HasCheckConstraint("ck_inventory_counts_scope_reference", "(scope_type = 'MaterialDomain' AND scope_material_domain_id IS NOT NULL) OR (scope_type <> 'MaterialDomain' AND scope_material_domain_id IS NULL)");
            table.HasCheckConstraint("ck_inventory_counts_timestamps", "(started_at_utc IS NULL OR started_at_utc >= planned_at_utc) AND (completed_at_utc IS NULL OR (started_at_utc IS NOT NULL AND completed_at_utc >= started_at_utc)) AND (closed_at_utc IS NULL OR (completed_at_utc IS NOT NULL AND closed_at_utc >= completed_at_utc))");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.CountType).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.ScopeType).HasConversion<string>().HasMaxLength(30);
        builder.Property(item => item.FreezePolicy).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.RowVersion).IsConcurrencyToken();
        builder.HasIndex(item => item.WarehouseId);
        builder.HasIndex(item => item.WarehouseId).HasFilter("status = 'InProgress'").IsUnique();
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(item => item.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MaterialDomain>().WithMany().HasForeignKey(item => item.ScopeMaterialDomainId).OnDelete(DeleteBehavior.Restrict);
    }
}
