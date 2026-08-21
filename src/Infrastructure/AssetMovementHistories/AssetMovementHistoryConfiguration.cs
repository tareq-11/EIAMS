using Domain.AssetMovementHistories;
using Domain.Assets;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.AssetMovementHistories;

internal sealed class AssetMovementHistoryConfiguration : IEntityTypeConfiguration<AssetMovementHistory>
{
    public void Configure(EntityTypeBuilder<AssetMovementHistory> builder)
    {
        builder.ToTable("asset_movement_history");
        builder.HasKey(history => history.Id);
        builder.Property(history => history.MovementType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.HasIndex(history => new { history.AssetId, history.MovedAtUtc, history.Id });
        builder.HasIndex(history => new { history.AssetId, history.DocumentId, history.MovementType }).IsUnique();
        builder.ToTable("asset_movement_history", table => table.HasCheckConstraint(
            "ck_asset_movement_history_type_valid",
            "movement_type IN ('Received', 'Transferred', 'Issued', 'Returned', 'Disposed')"));
        builder.HasOne<Asset>().WithMany().HasForeignKey(history => history.AssetId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<WarehouseDocument>().WithMany().HasForeignKey(history => history.DocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
