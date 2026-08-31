using Domain.DocumentLifecycleEvents;
using Domain.Users;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DocumentLifecycleEvents;

internal sealed class DocumentLifecycleEventConfiguration : IEntityTypeConfiguration<DocumentLifecycleEvent>
{
    public void Configure(EntityTypeBuilder<DocumentLifecycleEvent> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.FromStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.ToStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.Action).HasMaxLength(50).IsRequired();
        builder.Property(item => item.Reason).HasMaxLength(1000);
        builder.Property(item => item.ActorDisplayName).HasMaxLength(200).IsRequired();
        builder.Property(item => item.RequestId).HasMaxLength(200);

        builder.HasIndex(item => new { item.DocumentId, item.OccurredAtUtc, item.Id });
        builder.HasIndex(item => new { item.DocumentId, item.ResultingRowVersion, item.Action }).IsUnique();
        builder.HasIndex(item => new { item.DocumentId, item.Action, item.OperationId }).IsUnique();

        builder.ToTable("document_lifecycle_events", table =>
        {
            table.HasCheckConstraint("ck_document_lifecycle_events_action_not_blank", "btrim(action) <> ''");
            table.HasCheckConstraint("ck_document_lifecycle_events_actor_not_blank", "btrim(actor_display_name) <> ''");
            table.HasCheckConstraint("ck_document_lifecycle_events_row_version_positive", "resulting_row_version > 0");
        });

        builder.HasOne<WarehouseDocument>().WithMany()
            .HasForeignKey(item => item.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>().WithMany()
            .HasForeignKey(item => item.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
