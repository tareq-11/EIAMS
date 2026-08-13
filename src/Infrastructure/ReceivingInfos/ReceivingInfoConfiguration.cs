using Domain.ReceivingInfos;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.ReceivingInfos;

internal sealed class ReceivingInfoConfiguration : IEntityTypeConfiguration<ReceivingInfo>
{
    public void Configure(EntityTypeBuilder<ReceivingInfo> builder)
    {
        builder.ToTable("receiving_info");

        builder.HasKey(info => info.Id);

        builder.Property(info => info.Id).HasColumnName("document_id");

        builder.Property(info => info.SupplierRef).HasMaxLength(200).IsRequired();

        builder.Property(info => info.SupplierInvoiceRef).HasMaxLength(100);

        builder.Property(info => info.ReceivingType).HasConversion<string>().HasMaxLength(30);

        builder.ToTable("receiving_info", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_receiving_info_supplier_ref_not_blank",
                "length(btrim(supplier_ref)) > 0");
            tableBuilder.HasCheckConstraint(
                "ck_receiving_info_receiving_type_valid",
                "receiving_type IN ('Supplier', 'Transfer', 'Return')");
        });

        builder.HasOne<WarehouseDocument>().WithOne()
            .HasForeignKey<ReceivingInfo>(info => info.Id)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
