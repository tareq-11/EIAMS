using Domain.DocumentAttachments;
using Domain.Users;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DocumentAttachments;

internal sealed class DocumentAttachmentConfiguration : IEntityTypeConfiguration<DocumentAttachment>
{
    public void Configure(EntityTypeBuilder<DocumentAttachment> builder)
    {
        builder.HasKey(a => a.Id);

        builder.HasIndex(a => a.StorageKey).IsUnique();

        builder.HasIndex(a => new { a.DocumentId, a.AttachmentType })
            .HasDatabaseName("ux_document_attachments_signed_original")
            .IsUnique()
            .HasFilter("attachment_type = 'SignedOriginal'");

        builder.Property(a => a.AttachmentType).HasConversion<string>().HasMaxLength(20);

        builder.Property(a => a.StorageKey).HasMaxLength(500);

        builder.Property(a => a.OriginalFilename).HasMaxLength(300);

        builder.Property(a => a.MimeType).HasMaxLength(100);

        builder.Property(a => a.Checksum).HasMaxLength(128);

        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("ck_document_attachments_file_size_positive", "file_size > 0");
            tableBuilder.HasCheckConstraint(
                "ck_document_attachments_attachment_type_valid",
                "attachment_type IN ('SignedOriginal', 'Supporting')");
        });

        builder.HasOne<WarehouseDocument>().WithMany()
            .HasForeignKey(a => a.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UploadedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
