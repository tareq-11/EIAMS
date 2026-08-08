using Domain.MaterialDomains;
using Domain.WarehouseCapabilities;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.WarehouseCapabilities;

internal sealed class WarehouseCapabilityConfiguration : IEntityTypeConfiguration<WarehouseCapability>
{
    public void Configure(EntityTypeBuilder<WarehouseCapability> builder)
    {
        builder.HasKey(c => c.Id);

        builder.HasIndex(c => new { c.WarehouseId, c.MaterialDomainId }).IsUnique();

        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

        builder.ToTable(tableBuilder => tableBuilder.HasCheckConstraint(
            "ck_warehouse_capabilities_status_valid",
            "status IN ('Active', 'Inactive')"));

        builder.HasOne<Warehouse>().WithMany().HasForeignKey(c => c.WarehouseId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MaterialDomain>().WithMany()
            .HasForeignKey(c => c.MaterialDomainId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
