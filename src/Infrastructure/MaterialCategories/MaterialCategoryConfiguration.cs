using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.MaterialCategories;

internal sealed class MaterialCategoryConfiguration : IEntityTypeConfiguration<MaterialCategory>
{
    public void Configure(EntityTypeBuilder<MaterialCategory> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(200);

        builder.Property(c => c.Code).HasMaxLength(50);

        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<MaterialDomain>().WithMany().HasForeignKey(c => c.MaterialDomainId);

        builder.HasOne<MaterialCategory>().WithMany()
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
