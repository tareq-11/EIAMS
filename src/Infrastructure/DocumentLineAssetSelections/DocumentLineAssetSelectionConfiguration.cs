using Domain.Assets;
using Domain.DocumentLineAssetSelections;
using Domain.DocumentLines;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DocumentLineAssetSelections;

internal sealed class DocumentLineAssetSelectionConfiguration : IEntityTypeConfiguration<DocumentLineAssetSelection>
{
    public void Configure(EntityTypeBuilder<DocumentLineAssetSelection> builder)
    {
        builder.ToTable("document_line_asset_selections");
        builder.HasKey(selection => selection.Id);
        builder.HasIndex(selection => new { selection.DocumentLineId, selection.AssetId }).IsUnique();
        builder.HasIndex(selection => new { selection.DocumentId, selection.AssetId }).IsUnique();
        builder.HasIndex(selection => selection.AssetId);
        builder.HasOne<WarehouseDocument>().WithMany().HasForeignKey(selection => selection.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Asset>().WithMany().HasForeignKey(selection => selection.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentLine>().WithMany()
            .HasForeignKey(selection => new { selection.DocumentLineId, selection.DocumentId })
            .HasPrincipalKey(line => new { line.Id, line.DocumentId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
