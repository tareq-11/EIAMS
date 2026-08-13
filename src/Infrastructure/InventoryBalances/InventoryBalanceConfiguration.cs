using Domain.InventoryBalances;
using Domain.Materials;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.InventoryBalances;

internal sealed class InventoryBalanceConfiguration : IEntityTypeConfiguration<InventoryBalance>
{
    public void Configure(EntityTypeBuilder<InventoryBalance> builder)
    {
        builder.HasKey(b => b.Id);

        builder.HasIndex(b => new { b.WarehouseId, b.MaterialId }).IsUnique();

        builder.Property(b => b.Quantity).HasPrecision(18, 3);

        builder.Property(b => b.RowVersion).IsConcurrencyToken();

        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("ck_inventory_balances_quantity_non_negative", "quantity >= 0");
            tableBuilder.HasCheckConstraint("ck_inventory_balances_row_version_positive", "row_version > 0");
        });

        builder.HasOne<Warehouse>().WithMany().HasForeignKey(b => b.WarehouseId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Material>().WithMany().HasForeignKey(b => b.MaterialId).OnDelete(DeleteBehavior.Restrict);
    }
}
