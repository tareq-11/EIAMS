using Domain.DocumentLines;
using Domain.Materials;
using Domain.StockMovements;
using Domain.Users;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.StockMovements;

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.HasKey(m => m.Id);

        builder.HasIndex(m => new { m.DocumentId, m.LineId, m.MovementType }).IsUnique();

        builder.HasIndex(m => new { m.WarehouseId, m.MaterialId });

        builder.HasIndex(m => m.DocumentId);

        builder.Property(m => m.MovementType).HasConversion<string>().HasMaxLength(30);

        builder.Property(m => m.QuantityDelta).HasPrecision(18, 3);

        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("ck_stock_movements_quantity_delta_not_zero", "quantity_delta <> 0");
            tableBuilder.HasCheckConstraint(
                "ck_stock_movements_movement_type_valid",
                "movement_type IN " +
                "('Receipt', 'Issue', 'TransferIn', 'TransferOut', 'AdjustmentIn', 'AdjustmentOut', 'Opening')");
        });

        builder.HasOne<Warehouse>().WithMany().HasForeignKey(m => m.WarehouseId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Material>().WithMany().HasForeignKey(m => m.MaterialId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<WarehouseDocument>().WithMany()
            .HasForeignKey(m => m.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DocumentLine>().WithMany()
            .HasForeignKey(m => new { m.LineId, m.DocumentId, m.MaterialId })
            .HasPrincipalKey(l => new { l.Id, l.DocumentId, l.MaterialId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>().WithMany().HasForeignKey(m => m.PostedBy).OnDelete(DeleteBehavior.Restrict);

        // Append-only enforcement: a raw-SQL trigger rejecting UPDATE/DELETE is added in the M3
        // migration (EF's fluent configuration API has no first-class trigger support).
    }
}
