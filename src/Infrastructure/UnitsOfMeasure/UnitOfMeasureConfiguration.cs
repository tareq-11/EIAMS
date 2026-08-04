using Domain.UnitsOfMeasure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.UnitsOfMeasure;

internal sealed class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Name).HasMaxLength(100);

        builder.Property(u => u.Symbol).HasMaxLength(20);

        builder.Property(u => u.UnitType).HasMaxLength(50);
    }
}
