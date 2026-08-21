using Domain.DocumentLines;
using Domain.InventoryAdjustments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.InventoryAdjustments;

internal sealed class AdjustmentLineConfiguration : IEntityTypeConfiguration<AdjustmentLine>
{
    public void Configure(EntityTypeBuilder<AdjustmentLine> builder)
    {
        builder.ToTable("adjustment_lines", table =>
        {
            table.HasCheckConstraint("ck_adjustment_lines_difference_precision", "difference BETWEEN -999999999999999.999 AND 999999999999999.999");
            table.HasCheckConstraint("ck_adjustment_lines_reason_not_blank", "length(btrim(reason)) > 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Difference).HasPrecision(18, 3);
        builder.Property(item => item.Reason).HasMaxLength(200).IsRequired();
        builder.HasOne<InventoryAdjustment>().WithMany().HasForeignKey(item => item.AdjustmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DocumentLine>()
            .WithOne()
            .HasForeignKey<AdjustmentLine>(item => new { item.Id, item.AdjustmentId })
            .HasPrincipalKey<DocumentLine>(item => new { item.Id, item.DocumentId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
