using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.UnitsOfMeasure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Materials;

internal sealed class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.HasKey(m => m.Id);

        builder.HasIndex(m => m.Code).IsUnique();
        builder.HasIndex(m => m.FamilyId);
        builder.HasIndex(m => m.BaseUnitId);

        builder.Property(m => m.NameAr).HasMaxLength(500);

        builder.Property(m => m.NameEn).HasMaxLength(500);

        builder.Property(m => m.Code).HasMaxLength(100);

        builder.Property(m => m.MaterialKind).HasConversion<string>().HasMaxLength(20);

        builder.Property(m => m.TrackingType).HasConversion<string>().HasMaxLength(20);

        builder.Property(m => m.Attributes).HasColumnType("jsonb");

        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<MaterialFamily>().WithMany().HasForeignKey(m => m.FamilyId);

        builder.HasOne<UnitOfMeasure>().WithMany().HasForeignKey(m => m.BaseUnitId);

        // The classification revision is also the write-optimistic-concurrency token: a concurrent
        // classification write fails the conditional UPDATE instead of silently reusing a revision.
        builder.Property(m => m.CatalogVersion).IsConcurrencyToken();

        builder.ToTable(tableBuilder => tableBuilder.HasCheckConstraint(
            "ck_materials_catalog_version_positive",
            "catalog_version > 0"));
    }
}
