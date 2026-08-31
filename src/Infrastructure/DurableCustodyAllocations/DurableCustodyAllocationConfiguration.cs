using Domain.DurableCustodyAllocations;
using Domain.Materials;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.DurableCustodyAllocations;

internal sealed class DurableCustodyAllocationConfiguration : IEntityTypeConfiguration<DurableCustodyAllocation>
{
    public void Configure(EntityTypeBuilder<DurableCustodyAllocation> builder)
    {
        builder.ToTable("durable_custody_allocations");
        builder.HasKey(alloc => alloc.Id);

        builder.Property(alloc => alloc.HolderType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(alloc => alloc.CustodyKind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(alloc => alloc.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(alloc => alloc.IssuedQuantity).HasPrecision(18, 3).IsRequired();
        builder.Property(alloc => alloc.ActiveQuantity).HasPrecision(18, 3).IsRequired();
        builder.Property(alloc => alloc.ReturnedQuantity).HasPrecision(18, 3).IsRequired();
        builder.Property(alloc => alloc.RowVersion).IsConcurrencyToken();

        builder.HasIndex(alloc => new { alloc.HolderType, alloc.HolderId });
        builder.HasIndex(alloc => alloc.MaterialId);
        builder.HasIndex(alloc => alloc.WarehouseId);
        builder.HasIndex(alloc => alloc.IssueDocumentId);
        builder.HasIndex(alloc => alloc.Status);

        builder.ToTable("durable_custody_allocations", table =>
        {
            table.HasCheckConstraint("ck_durable_alloc_holder_type_valid", "holder_type IN ('Employee', 'OrganizationalUnit', 'Site', 'External')");
            table.HasCheckConstraint("ck_durable_alloc_kind_valid", "custody_kind IN ('Operational', 'Personal')");
            table.HasCheckConstraint("ck_durable_alloc_personal_requires_employee", "custody_kind <> 'Personal' OR holder_type = 'Employee'");
            table.HasCheckConstraint("ck_durable_alloc_operational_requires_non_employee", "custody_kind <> 'Operational' OR holder_type <> 'Employee'");
            table.HasCheckConstraint("ck_durable_alloc_status_valid", "status IN ('Active', 'FullyReturned')");
            table.HasCheckConstraint("ck_durable_alloc_row_version_positive", "row_version > 0");
            table.HasCheckConstraint(
                "ck_durable_alloc_quantities",
                "issued_quantity > 0 AND active_quantity >= 0 AND returned_quantity >= 0 AND (active_quantity + returned_quantity = issued_quantity)");
        });

        builder.HasOne<Material>().WithMany().HasForeignKey(alloc => alloc.MaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(alloc => alloc.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WarehouseDocument>().WithMany().HasForeignKey(alloc => alloc.IssueDocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
