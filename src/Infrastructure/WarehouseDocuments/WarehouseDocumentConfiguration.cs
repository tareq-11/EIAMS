using Domain.DocumentAttachments;
using Domain.Users;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.WarehouseDocuments;

internal sealed class WarehouseDocumentConfiguration : IEntityTypeConfiguration<WarehouseDocument>
{
    public void Configure(EntityTypeBuilder<WarehouseDocument> builder)
    {
        builder.HasKey(d => d.Id);

        builder.HasIndex(d => d.SystemReferenceNumber).IsUnique();

        builder.HasIndex(d => d.ReversalOfDocumentId)
            .IsUnique()
            .HasFilter("reversal_of_document_id IS NOT NULL");

        builder.Property(d => d.DocumentType).HasConversion<string>().HasMaxLength(30);

        builder.Property(d => d.DocumentStatus).HasConversion<string>().HasMaxLength(20);

        builder.Property(d => d.PaperDocumentNumber).HasMaxLength(100);

        builder.Property(d => d.SystemReferenceNumber).HasMaxLength(100);

        builder.Property(d => d.RowVersion).IsConcurrencyToken();

        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_warehouse_documents_document_type_valid",
                "document_type IN ('Receiving', 'Issue', 'Transfer', 'Adjustment', 'Opening', 'Return')");

            tableBuilder.HasCheckConstraint(
                "ck_warehouse_documents_document_status_valid",
                "document_status IN ('Draft', 'Submitted', 'Posted', 'Reversed', 'Cancelled', 'Rejected')");

            tableBuilder.HasCheckConstraint(
                "ck_warehouse_documents_row_version_positive",
                "row_version > 0");

            tableBuilder.HasCheckConstraint(
                "ck_warehouse_documents_paper_document_year_valid",
                "paper_document_year IS NULL OR paper_document_year BETWEEN 1900 AND 9999");

            tableBuilder.HasCheckConstraint(
                "ck_warehouse_documents_posted_metadata",
                "(document_status IN ('Posted', 'Reversed') " +
                "AND posted_by IS NOT NULL AND posted_at_utc IS NOT NULL AND signed_copy_attachment_id IS NOT NULL) " +
                "OR (document_status NOT IN ('Posted', 'Reversed') " +
                "AND posted_by IS NULL AND posted_at_utc IS NULL)");
        });

        builder.HasOne<Warehouse>().WithMany().HasForeignKey(d => d.WarehouseId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>().WithMany().HasForeignKey(d => d.CreatedBy).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>().WithMany().HasForeignKey(d => d.PostedBy).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<WarehouseDocument>().WithMany()
            .HasForeignKey(d => d.ReversalOfDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DocumentAttachment>().WithMany()
            .HasForeignKey(d => d.SignedCopyAttachmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
