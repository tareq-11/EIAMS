using Domain.AuditLogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.AuditLogs;

internal sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.FieldName).HasMaxLength(AuditLogEntry.MaxFieldNameLength).IsRequired();

        builder.HasIndex(entry => new { entry.AuditLogId, entry.FieldName, entry.Id });

        builder.HasIndex(entry => new { entry.FieldName, entry.AuditLogId });

        builder.ToTable("audit_log_entries", table =>
        {
            table.HasCheckConstraint("ck_audit_log_entries_field_name_not_blank", "btrim(field_name) <> ''");
            table.HasCheckConstraint("ck_audit_log_entries_values_distinct", "old_value IS DISTINCT FROM new_value");
        });

        builder.HasOne<AuditLog>().WithMany()
            .HasForeignKey(entry => entry.AuditLogId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
