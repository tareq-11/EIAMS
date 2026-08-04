using Domain.MaterialDomains;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.MaterialDomains;

internal sealed class MaterialDomainConfiguration : IEntityTypeConfiguration<MaterialDomain>
{
    public void Configure(EntityTypeBuilder<MaterialDomain> builder)
    {
        builder.HasKey(d => d.Id);

        builder.HasIndex(d => d.Code).IsUnique();

        builder.Property(d => d.Name).HasMaxLength(200);

        builder.Property(d => d.Code).HasMaxLength(50);

        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
    }
}
