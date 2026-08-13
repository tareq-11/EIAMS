using Domain.IssueTos;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.IssueTos;

internal sealed class IssueToConfiguration : IEntityTypeConfiguration<IssueTo>
{
    public void Configure(EntityTypeBuilder<IssueTo> builder)
    {
        builder.ToTable("issue_to");

        builder.HasKey(issueTo => issueTo.Id);

        builder.Property(issueTo => issueTo.Id).HasColumnName("document_id");

        builder.Property(issueTo => issueTo.RecipientType).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(issueTo => issueTo.RecipientId).IsRequired();

        builder.Property(issueTo => issueTo.IssueReason).HasMaxLength(200).IsRequired();

        builder.HasIndex(issueTo => new { issueTo.RecipientType, issueTo.RecipientId });

        builder.ToTable("issue_to", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_issue_to_recipient_type_valid",
                "recipient_type IN ('Employee', 'OrganizationalUnit', 'Site', 'External')");
            tableBuilder.HasCheckConstraint(
                "ck_issue_to_issue_reason_not_blank",
                "length(btrim(issue_reason)) > 0");
        });

        builder.HasOne<WarehouseDocument>().WithOne()
            .HasForeignKey<IssueTo>(issueTo => issueTo.Id)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
