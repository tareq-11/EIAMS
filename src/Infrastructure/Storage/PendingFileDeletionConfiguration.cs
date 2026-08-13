using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Storage;

internal sealed class PendingFileDeletionConfiguration : IEntityTypeConfiguration<PendingFileDeletion>
{
    public void Configure(EntityTypeBuilder<PendingFileDeletion> builder)
    {
        builder.HasKey(item => item.Id);

        builder.HasIndex(item => item.StorageKey).IsUnique();
        builder.HasIndex(item => item.NextAttemptAtUtc);

        builder.Property(item => item.StorageKey).HasMaxLength(200);
        builder.Property(item => item.LastError).HasMaxLength(PendingFileDeletion.MaxLastErrorLength);

        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_pending_file_deletions_attempt_count_non_negative",
                "attempt_count >= 0");
        });
    }
}
