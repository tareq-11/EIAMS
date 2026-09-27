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

        builder.HasIndex(l => l.SourceConversionId);

        builder.HasIndex(l => new { l.DocumentId, l.SourceMaterialVersion });

        builder.HasIndex(l => l.SourceLineId).IsUnique().HasFilter("source_line_id IS NOT NULL");

        builder.HasAlternateKey(l => new { l.Id, l.DocumentId, l.MaterialId });

        builder.HasAlternateKey(l => new { l.Id, l.MaterialId });

        builder.Property(l => l.LineType).HasConversion<string>().HasMaxLength(20);

        builder.Property(l => l.OpeningType).HasConversion<string>().HasMaxLength(20);

        builder.Property(l => l.SourceMaterialKind).HasConversion<string>().HasMaxLength(20);

        builder.Property(l => l.SourceTrackingType).HasConversion<string>().HasMaxLength(20);

        builder.Property(l => l.SourceConversionFactor).HasPrecision(18, 6);

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
            tableBuilder.HasCheckConstraint(
                "ck_document_lines_source_conversion_complete",
                "(source_conversion_id IS NULL AND source_conversion_from_unit_id IS NULL AND " +
                "source_conversion_to_unit_id IS NULL AND source_conversion_factor IS NULL) OR " +
                "(source_conversion_id IS NOT NULL AND source_conversion_from_unit_id IS NOT NULL AND " +
                "source_conversion_to_unit_id IS NOT NULL AND source_conversion_factor IS NOT NULL AND " +
                "source_conversion_factor > 0)");
            tableBuilder.HasCheckConstraint(
                "ck_document_lines_source_conversion_required",
                "source_material_version IS NULL OR source_base_unit_id IS NULL OR unit_id IS NULL OR " +
                "unit_id = source_base_unit_id OR source_conversion_id IS NOT NULL");
            tableBuilder.HasCheckConstraint(
                "ck_document_lines_source_material_version_positive",
                "source_material_version IS NULL OR source_material_version > 0");

            // The captured snapshot is all-or-nothing: a revision without its classification and base
            // unit, or the other way round, can never be interpreted, so a partial capture is refused
            // by the database and not only by the domain. Every legacy line keeps all four columns
            // null, which this constraint accepts as the explicit "never captured" state; such a line
            // is refused at submit/post rather than re-interpreted against the live catalog.
            tableBuilder.HasCheckConstraint(
                "ck_document_lines_source_provenance_complete",
                "(source_material_version IS NULL AND source_material_kind IS NULL AND " +
                "source_tracking_type IS NULL AND source_base_unit_id IS NULL) OR " +
                "(source_material_version IS NOT NULL AND source_material_version > 0 AND " +
                "source_material_kind IS NOT NULL AND source_material_kind IN ('Consumable', 'Durable', 'Asset') AND " +
                "source_tracking_type IS NOT NULL AND source_tracking_type IN ('Quantity', 'Serial') AND " +
                "source_base_unit_id IS NOT NULL AND source_base_unit_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
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
