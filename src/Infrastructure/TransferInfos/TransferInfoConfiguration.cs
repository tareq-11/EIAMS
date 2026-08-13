using Domain.TransferInfos;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.TransferInfos;

internal sealed class TransferInfoConfiguration : IEntityTypeConfiguration<TransferInfo>
{
    public void Configure(EntityTypeBuilder<TransferInfo> builder)
    {
        builder.ToTable("transfer_info");

        builder.HasKey(transferInfo => transferInfo.Id);

        builder.Property(transferInfo => transferInfo.Id).HasColumnName("document_id");

        builder.Property(transferInfo => transferInfo.TransferReason).HasMaxLength(200).IsRequired();

        builder.HasIndex(transferInfo => transferInfo.DestinationWarehouseId);

        builder.ToTable("transfer_info", tableBuilder => tableBuilder.HasCheckConstraint(
            "ck_transfer_info_transfer_reason_not_blank",
            "length(btrim(transfer_reason)) > 0"));

        builder.HasOne<WarehouseDocument>().WithOne()
            .HasForeignKey<TransferInfo>(transferInfo => transferInfo.Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(transferInfo => transferInfo.DestinationWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
