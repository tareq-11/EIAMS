using Domain.DurableCustodies;
using Domain.Users;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DurableCustodies;

internal sealed class DurableCustodyHistoryConfiguration : IEntityTypeConfiguration<DurableCustodyHistory>
{
    public void Configure(EntityTypeBuilder<DurableCustodyHistory> builder)
    {
        builder.ToTable("durable_custody_histories");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.SubjectType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(h => h.Action).HasMaxLength(50).IsRequired();
        builder.Property(h => h.FromHolderType).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.ToHolderType).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.Quantity).HasPrecision(18, 3);
        builder.Property(h => h.Note).HasMaxLength(300);

        builder.HasIndex(h => new { h.SubjectType, h.SubjectId });
        builder.HasIndex(h => h.TimestampUtc);

        builder.ToTable("durable_custody_histories", table =>
        {
            table.HasCheckConstraint("ck_durable_history_subject_type", "subject_type IN ('TrackedUnit', 'MaterialQuantity')");
            table.HasCheckConstraint("ck_durable_history_action_valid", "action IN ('Issued', 'Returned', 'Transferred')");
        });

        builder.HasOne<WarehouseDocument>().WithMany().HasForeignKey(h => h.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(h => h.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}
