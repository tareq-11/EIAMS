using Domain.InventoryCounts;
using Domain.Materials;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.InventoryCounts;

internal sealed class InventoryCountScopeMaterialConfiguration : IEntityTypeConfiguration<InventoryCountScopeMaterial>
{
    public void Configure(EntityTypeBuilder<InventoryCountScopeMaterial> builder)
    {
        builder.ToTable("inventory_count_scope_materials");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.CountId, item.MaterialId }).IsUnique();
        builder.HasOne<InventoryCount>().WithMany().HasForeignKey(item => item.CountId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Material>().WithMany().HasForeignKey(item => item.MaterialId).OnDelete(DeleteBehavior.Restrict);
    }
}
