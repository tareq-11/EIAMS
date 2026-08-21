using Domain.ReturnInfos;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.ReturnInfos;

internal sealed class ReturnInfoConfiguration : IEntityTypeConfiguration<ReturnInfo>
{
    public void Configure(EntityTypeBuilder<ReturnInfo> builder)
    {
        builder.ToTable("return_info");
        builder.HasKey(info => info.Id);
        builder.Property(info => info.Id).HasColumnName("document_id");
        builder.Property(info => info.ReturnReason).HasMaxLength(200).IsRequired();
        builder.HasIndex(info => info.OriginalIssueDocumentId);
        builder.ToTable("return_info", table => table.HasCheckConstraint(
            "ck_return_info_return_reason_not_blank",
            "length(btrim(return_reason)) > 0"));
        builder.HasOne<WarehouseDocument>().WithOne().HasForeignKey<ReturnInfo>(info => info.Id).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WarehouseDocument>().WithMany().HasForeignKey(info => info.OriginalIssueDocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
