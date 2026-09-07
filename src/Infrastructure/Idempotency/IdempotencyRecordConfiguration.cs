using Domain.Idempotency;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Idempotency;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.HasKey(record => record.Key);
        builder.Property(record => record.Operation).HasMaxLength(100).IsRequired();
        builder.Property(record => record.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(record => record.ResponsePayload).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(record => record.ExpiresAtUtc);
        builder.HasOne<User>().WithMany()
            .HasForeignKey(record => record.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("idempotency_records", table =>
        {
            table.HasCheckConstraint("ck_idempotency_records_operation_not_blank", "btrim(operation) <> ''");
            table.HasCheckConstraint("ck_idempotency_records_request_hash_length", "length(request_hash) = 64");
            table.HasCheckConstraint("ck_idempotency_records_expiry", "expires_at_utc > created_at_utc");
        });
    }
}
