using Domain.OrganizationalUnits;
using Domain.Sites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.OrganizationalUnits;

internal sealed class OrganizationalUnitConfiguration : IEntityTypeConfiguration<OrganizationalUnit>
{
    public void Configure(EntityTypeBuilder<OrganizationalUnit> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Name).HasMaxLength(200);

        builder.Property(u => u.UnitType).HasMaxLength(50);

        builder.Property(u => u.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<Site>().WithMany().HasForeignKey(u => u.SiteId);

        builder.HasOne<OrganizationalUnit>().WithMany()
            .HasForeignKey(u => u.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
