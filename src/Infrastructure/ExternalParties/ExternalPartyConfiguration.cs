using Domain.ExternalParties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.ExternalParties;

internal sealed class ExternalPartyConfiguration : IEntityTypeConfiguration<ExternalParty>
{
    public void Configure(EntityTypeBuilder<ExternalParty> builder)
    {
        builder.HasKey(party => party.Id);

        builder.Property(party => party.NameAr)
            .HasMaxLength(ExternalParty.MaxNameLength)
            .IsRequired();

        builder.Property(party => party.NormalizedNameAr)
            .HasMaxLength(ExternalParty.MaxNameLength)
            .IsRequired();

        builder.HasIndex(party => party.NormalizedNameAr).IsUnique();

        builder.Property(party => party.Code).HasMaxLength(ExternalParty.MaxCodeLength);
        builder.Property(party => party.NormalizedCode).HasMaxLength(ExternalParty.MaxCodeLength);
        builder.HasIndex(party => party.NormalizedCode)
            .IsUnique()
            .HasFilter("normalized_code IS NOT NULL");

        builder.Property(party => party.ContactInfo).HasMaxLength(ExternalParty.MaxContactInfoLength);
        builder.Property(party => party.Notes).HasMaxLength(ExternalParty.MaxNotesLength);
        builder.Property(party => party.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(party => party.RowVersion).IsConcurrencyToken();

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_external_parties_name_not_blank",
                "length(btrim(name_ar)) > 0");
            table.HasCheckConstraint(
                "ck_external_parties_status_valid",
                "status IN ('Active', 'Inactive')");
            table.HasCheckConstraint(
                "ck_external_parties_row_version_positive",
                "row_version > 0");
        });
    }
}
