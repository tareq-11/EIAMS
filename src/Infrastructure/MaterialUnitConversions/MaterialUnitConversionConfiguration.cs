using Domain.Materials;
using Domain.MaterialUnitConversions;
using Domain.UnitsOfMeasure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.MaterialUnitConversions;

internal sealed class MaterialUnitConversionConfiguration : IEntityTypeConfiguration<MaterialUnitConversion>
{
    public void Configure(EntityTypeBuilder<MaterialUnitConversion> builder)
    {
        builder.HasKey(c => c.Id);

        builder.HasIndex(c => new { c.MaterialId, c.FromUnitId }).IsUnique();

        builder.Property(c => c.Factor).HasPrecision(18, 6);

        builder.ToTable(tableBuilder => tableBuilder.HasCheckConstraint(
            "ck_material_unit_conversions_positive_factor",
            "factor > 0"));

        builder.HasOne<Material>().WithMany().HasForeignKey(c => c.MaterialId);

        builder.HasOne<UnitOfMeasure>().WithMany()
            .HasForeignKey(c => c.FromUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UnitOfMeasure>().WithMany()
            .HasForeignKey(c => c.ToBaseUnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
