using Domain.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Permissions;

internal sealed class AuthorizationPolicyVersionConfiguration : IEntityTypeConfiguration<AuthorizationPolicyVersion>
{
    public void Configure(EntityTypeBuilder<AuthorizationPolicyVersion> builder)
    {
        builder.ToTable("authorization_policy_versions");
        builder.HasKey(version => version.Id);
        builder.Property(version => version.ActiveVocabulary).HasMaxLength(50).IsRequired();
        builder.Property(version => version.MappingVersion).IsRequired();
        builder.Property(version => version.ConcurrencyToken).IsConcurrencyToken().IsRequired();
        builder.Property(version => version.ActivatedAtUtc).IsRequired();
        builder.Property(version => version.ActivatedBy).HasMaxLength(200).IsRequired();
        builder.Property(version => version.IsActive).IsRequired();
        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_authorization_policy_versions_mapping_version_positive",
                "mapping_version > 0");
            tableBuilder.HasCheckConstraint(
                "ck_authorization_policy_versions_concurrency_token_positive",
                "concurrency_token > 0");
            tableBuilder.HasCheckConstraint(
                "ck_authorization_policy_versions_vocabulary_supported",
                "active_vocabulary IN ('legacy-colon', 'dotted-v1')");
            tableBuilder.HasCheckConstraint(
                "ck_authorization_policy_versions_strings_non_empty",
                "length(trim(active_vocabulary)) > 0 AND length(trim(activated_by)) > 0");
        });
        builder.HasIndex(version => version.IsActive)
            .IsUnique()
            .HasFilter("is_active = true")
            .HasDatabaseName("ux_authorization_policy_versions_active");

        builder.HasData(new
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000901"),
            ActiveVocabulary = "legacy-colon",
            MappingVersion = 1,
            ConcurrencyToken = 1L,
            ActivatedAtUtc = new DateTime(2026, 9, 13, 0, 0, 0, DateTimeKind.Utc),
            ActivatedBy = "phase-1-expand",
            IsActive = false
        }, new
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000902"),
            ActiveVocabulary = "dotted-v1",
            MappingVersion = 1,
            ConcurrencyToken = 2L,
            ActivatedAtUtc = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc),
            ActivatedBy = "phase-5-cutover",
            IsActive = true
        });
    }
}
