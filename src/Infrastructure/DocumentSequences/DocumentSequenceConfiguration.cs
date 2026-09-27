using Domain.DocumentSequences;
using Domain.Sites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DocumentSequences;

internal sealed class DocumentSequenceConfiguration : IEntityTypeConfiguration<DocumentSequence>
{
    public void Configure(EntityTypeBuilder<DocumentSequence> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasIndex(s => new { s.SiteId, s.Year }).IsUnique();

        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("ck_document_sequences_year_valid", "year >= 2000");
            tableBuilder.HasCheckConstraint("ck_document_sequences_last_sequence_non_negative", "last_sequence >= 0");
        });

        builder.HasOne<Site>().WithMany().HasForeignKey(s => s.SiteId).OnDelete(DeleteBehavior.Restrict);
    }
}
