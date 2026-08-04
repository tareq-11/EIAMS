using Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Organizations;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.HasKey(o => o.Id);

        builder.HasIndex(o => o.Code).IsUnique();

        builder.Property(o => o.Name).HasMaxLength(200);

        builder.Property(o => o.Code).HasMaxLength(50);

        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
    }
}
