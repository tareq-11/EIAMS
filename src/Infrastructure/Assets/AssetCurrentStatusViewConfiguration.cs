using Domain.Assets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Assets;

internal sealed class AssetCurrentStatusViewConfiguration : IEntityTypeConfiguration<AssetCurrentStatusView>
{
    public void Configure(EntityTypeBuilder<AssetCurrentStatusView> builder)
    {
        builder.HasNoKey();
        builder.ToView("v_asset_current_status");
        builder.Property(view => view.AssetId).HasColumnName("asset_id");
        builder.Property(view => view.MaterialId).HasColumnName("material_id");
        builder.Property(view => view.WarehouseId).HasColumnName("warehouse_id");
        builder.Property(view => view.AssetNumber).HasColumnName("asset_number");
        builder.Property(view => view.SerialNumber).HasColumnName("serial_number");
        builder.Property(view => view.CurrentStatus).HasColumnName("current_status").HasConversion<string>();
        builder.Property(view => view.ActiveCustodyId).HasColumnName("active_custody_id");
        builder.Property(view => view.HolderType).HasColumnName("holder_type").HasConversion<string>();
        builder.Property(view => view.HolderId).HasColumnName("holder_id");
        builder.Property(view => view.CustodyKind).HasColumnName("custody_kind").HasConversion<string>();
        builder.Property(view => view.LatestMovementType).HasColumnName("latest_movement_type").HasConversion<string>();
        builder.Property(view => view.LatestMovementAtUtc).HasColumnName("latest_movement_at_utc");
    }
}
