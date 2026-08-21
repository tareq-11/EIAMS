using Domain.WarehouseCapabilities;
using Domain.WarehouseCapabilityOperations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.WarehouseCapabilityOperations;

internal sealed class WarehouseCapabilityOperationConfiguration : IEntityTypeConfiguration<WarehouseCapabilityOperation>
{
    public void Configure(EntityTypeBuilder<WarehouseCapabilityOperation> builder)
    {
        builder.HasKey(o => o.Id);

        builder.HasIndex(o => new { o.CapabilityId, o.OperationType }).IsUnique();

        builder.Property(o => o.OperationType).HasConversion<string>().HasMaxLength(20);

        builder.ToTable(tableBuilder => tableBuilder.HasCheckConstraint(
            "ck_warehouse_capability_operations_operation_type_valid",
            "operation_type IN ('Receiving', 'Issue', 'Transfer', 'Count', 'Return', 'Adjustment')"));

        builder.HasOne<WarehouseCapability>().WithMany()
            .HasForeignKey(o => o.CapabilityId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
