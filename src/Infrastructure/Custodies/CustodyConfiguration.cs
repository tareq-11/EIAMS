using Domain.Assets;
using Domain.Custodies;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Custodies;

internal sealed class CustodyConfiguration : IEntityTypeConfiguration<Custody>
{
    public void Configure(EntityTypeBuilder<Custody> builder)
    {
        builder.ToTable("custodies");
        builder.HasKey(custody => custody.Id);
        builder.Property(custody => custody.HolderType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(custody => custody.CustodyKind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(custody => custody.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(custody => custody.RowVersion).IsConcurrencyToken();
        builder.HasIndex(custody => new { custody.AssetId, custody.Status });
        builder.HasIndex(custody => new { custody.HolderType, custody.HolderId });
        builder.HasIndex(custody => custody.AssetId).HasFilter("status = 'Active'").IsUnique();
        builder.ToTable("custodies", table =>
        {
            table.HasCheckConstraint("ck_custodies_holder_type_valid", "holder_type IN ('Employee', 'OrganizationalUnit', 'Site', 'External')");
            table.HasCheckConstraint("ck_custodies_kind_valid", "custody_kind IN ('Operational', 'Personal')");
            table.HasCheckConstraint(
                "ck_custodies_personal_requires_employee",
                "custody_kind <> 'Personal' OR holder_type = 'Employee'");
            table.HasCheckConstraint("ck_custodies_status_valid", "status IN ('Active', 'Closed')");
            table.HasCheckConstraint("ck_custodies_row_version_positive", "row_version > 0");
            table.HasCheckConstraint(
                "ck_custodies_state_time_valid",
                "(status = 'Active' AND to_utc IS NULL AND return_document_id IS NULL AND disposal_document_id IS NULL) OR (status = 'Closed' AND to_utc IS NOT NULL AND from_utc < to_utc AND NOT (return_document_id IS NOT NULL AND disposal_document_id IS NOT NULL))");
        });
        builder.HasOne<Asset>().WithMany().HasForeignKey(custody => custody.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WarehouseDocument>().WithMany().HasForeignKey(custody => custody.IssueDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WarehouseDocument>().WithMany().HasForeignKey(custody => custody.ReturnDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WarehouseDocument>().WithMany().HasForeignKey(custody => custody.DisposalDocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
