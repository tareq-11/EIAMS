using Domain.DocumentLines;
using Domain.Materials;
using Domain.UnitsOfMeasure;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DocumentLines;

internal sealed class DocumentLineConfiguration : IEntityTypeConfiguration<DocumentLine>
{
    public void Configure(EntityTypeBuilder<DocumentLine> builder)
    {
        builder.HasKey(l => l.Id);

        builder.HasIndex(l => l.DocumentId);

        builder.HasIndex(l => new { l.DocumentId, l.MaterialId });

        builder.HasIndex(l => l.SourceLineId).IsUnique().HasFilter("source_line_id IS NOT NULL");

        builder.HasAlternateKey(l => new { l.Id, l.DocumentId, l.MaterialId });

        builder.HasAlternateKey(l => new { l.Id, l.MaterialId });

        builder.Property(l => l.LineType).HasConversion<string>().HasMaxLength(20);

        builder.Property(l => l.OpeningType).HasConversion<string>().HasMaxLength(20);

        builder.Property(l => l.Quantity).HasPrecision(18, 3);

        builder.Property(l => l.BaseQuantity).HasPrecision(18, 3);

        builder.Property(l => l.UnitPrice).HasPrecision(18, 2);

        builder.Property(l => l.BatchNumber).HasMaxLength(100);

        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("ck_document_lines_quantity_positive", "quantity > 0");
            tableBuilder.HasCheckConstraint("ck_document_lines_base_quantity_positive", "base_quantity > 0");
            tableBuilder.HasCheckConstraint("ck_document_lines_unit_price_non_negative", "unit_price >= 0");
            tableBuilder.HasCheckConstraint("ck_document_lines_line_type_valid", "line_type IN ('Normal', 'Asset')");
            tableBuilder.HasCheckConstraint(
                "ck_document_lines_opening_type_valid",
                "opening_type IS NULL OR opening_type IN ('Initial', 'Correction')");
        });

        builder.HasOne<WarehouseDocument>().WithMany()
            .HasForeignKey(l => l.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DocumentLine>().WithMany()
            .HasForeignKey(l => l.SourceLineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Material>().WithMany().HasForeignKey(l => l.MaterialId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UnitOfMeasure>().WithMany().HasForeignKey(l => l.UnitId).OnDelete(DeleteBehavior.Restrict);
    }
}
