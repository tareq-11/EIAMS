using Domain.Assets;
using Domain.DocumentLines;
using Domain.Materials;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Assets;

internal sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.HasKey(asset => asset.Id);

        builder.HasIndex(asset => asset.AssetNumber).IsUnique();

        builder.HasIndex(asset => asset.ReceiptLineId);

        builder.HasIndex(asset => new { asset.WarehouseId, asset.MaterialId });

        builder.Property(asset => asset.AssetNumber).HasMaxLength(100).IsRequired();

        builder.Property(asset => asset.SerialNumber).HasMaxLength(200);

        builder.Property(asset => asset.RowVersion).IsConcurrencyToken();

        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_assets_asset_number_not_blank",
                "length(btrim(asset_number)) > 0");
            tableBuilder.HasCheckConstraint(
                "ck_assets_warranty_after_acquisition",
                "warranty_expiry IS NULL OR warranty_expiry >= acquisition_date");
            tableBuilder.HasCheckConstraint("ck_assets_row_version_positive", "row_version > 0");
        });

        builder.HasOne<Material>().WithMany()
            .HasForeignKey(asset => asset.MaterialId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(asset => asset.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DocumentLine>().WithMany()
            .HasForeignKey(asset => new { asset.ReceiptLineId, asset.MaterialId })
            .HasPrincipalKey(line => new { line.Id, line.MaterialId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
