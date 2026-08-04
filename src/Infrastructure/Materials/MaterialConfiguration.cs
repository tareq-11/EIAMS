using Domain.MaterialFamilies;
using Domain.Materials;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Materials;

internal sealed class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.HasKey(m => m.Id);

        builder.HasIndex(m => m.Code).IsUnique();

        builder.Property(m => m.NameAr).HasMaxLength(500);

        builder.Property(m => m.NameEn).HasMaxLength(500);

        builder.Property(m => m.Code).HasMaxLength(100);

        builder.Property(m => m.MaterialKind).HasConversion<string>().HasMaxLength(20);

        builder.Property(m => m.TrackingType).HasConversion<string>().HasMaxLength(20);

        builder.Property(m => m.Attributes).HasColumnType("jsonb");

        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<MaterialFamily>().WithMany().HasForeignKey(m => m.FamilyId);
    }
}
