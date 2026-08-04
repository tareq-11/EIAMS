using Domain.MaterialCategories;
using Domain.MaterialFamilies;
using Domain.UnitsOfMeasure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.MaterialFamilies;

internal sealed class MaterialFamilyConfiguration : IEntityTypeConfiguration<MaterialFamily>
{
    public void Configure(EntityTypeBuilder<MaterialFamily> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name).HasMaxLength(200);

        builder.Property(f => f.Code).HasMaxLength(50);

        builder.Property(f => f.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<MaterialCategory>().WithMany().HasForeignKey(f => f.CategoryId);

        builder.HasOne<UnitOfMeasure>().WithMany().HasForeignKey(f => f.BaseUnitId);
    }
}
