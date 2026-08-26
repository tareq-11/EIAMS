using Domain.AuditLogs;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.AuditLogs;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(auditLog => auditLog.Id);

        builder.Property(auditLog => auditLog.RequestId).HasMaxLength(AuditLog.MaxRequestIdLength);

        builder.Property(auditLog => auditLog.EntityType).HasMaxLength(AuditLog.MaxEntityTypeLength).IsRequired();

        builder.Property(auditLog => auditLog.AggregateType).HasMaxLength(AuditLog.MaxEntityTypeLength);

        builder.Property(auditLog => auditLog.Action).HasMaxLength(AuditLog.MaxActionLength).IsRequired();

        builder.Property(auditLog => auditLog.CommandName).HasMaxLength(AuditLog.MaxCommandNameLength);

        builder.Property(auditLog => auditLog.IpAddress).HasMaxLength(AuditLog.MaxIpAddressLength);

        builder.Property(auditLog => auditLog.Summary).HasColumnType("jsonb");

        builder.HasIndex(auditLog => new { auditLog.EntityType, auditLog.EntityId, auditLog.CreatedAtUtc, auditLog.Id })
            .IsDescending(false, false, true, true);

        builder.HasIndex(auditLog => new { auditLog.AggregateType, auditLog.AggregateId, auditLog.CreatedAtUtc, auditLog.Id })
            .IsDescending(false, false, true, true);

        builder.HasIndex(auditLog => new { auditLog.UserId, auditLog.CreatedAtUtc, auditLog.Id })
            .IsDescending(false, true, true)
            .HasFilter("user_id IS NOT NULL");

        builder.HasIndex(auditLog => new { auditLog.OperationId, auditLog.CreatedAtUtc, auditLog.Id });

        builder.HasIndex(auditLog => auditLog.RequestId)
            .HasFilter("request_id IS NOT NULL");

        builder.HasIndex(auditLog => new { auditLog.CreatedAtUtc, auditLog.Id })
            .IsDescending(true, true);

        builder.ToTable("audit_logs", table =>
        {
            table.HasCheckConstraint("ck_audit_logs_entity_type_not_blank", "btrim(entity_type) <> ''");
            table.HasCheckConstraint("ck_audit_logs_action_not_blank", "btrim(action) <> ''");
            table.HasCheckConstraint(
                "ck_audit_logs_summary_is_json_object",
                "summary IS NULL OR jsonb_typeof(summary) = 'object'");
            table.HasCheckConstraint(
                "ck_audit_logs_summary_max_bytes",
                "summary IS NULL OR octet_length(summary::text) <= 16384");
        });

        builder.HasOne<User>().WithMany().HasForeignKey(auditLog => auditLog.UserId).OnDelete(DeleteBehavior.Restrict);

        // Append-only enforcement: a raw-SQL trigger rejecting UPDATE/DELETE is added in the M8
        // migration (EF's fluent configuration API has no first-class trigger support).
    }
}
