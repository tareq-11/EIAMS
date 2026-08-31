using Domain.Materials;
using Domain.TrackedMaterialUnits;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.TrackedMaterialUnits;

internal sealed class TrackedMaterialUnitConfiguration : IEntityTypeConfiguration<TrackedMaterialUnit>
{
    public void Configure(EntityTypeBuilder<TrackedMaterialUnit> builder)
    {
        builder.ToTable("tracked_material_units");
        builder.HasKey(unit => unit.Id);

        builder.Property(unit => unit.SerialNumber).HasMaxLength(100).IsRequired();
        builder.Property(unit => unit.HolderType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(unit => unit.CustodyKind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(unit => unit.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(unit => unit.RowVersion).IsConcurrencyToken();

        builder.HasIndex(unit => new { unit.MaterialId, unit.SerialNumber })
            .HasDatabaseName("ux_tracked_material_units_active_serial")
            .IsUnique()
            .HasFilter("status = 'Issued'");

        builder.HasIndex(unit => new { unit.HolderType, unit.HolderId });
        builder.HasIndex(unit => unit.WarehouseId);
        builder.HasIndex(unit => unit.IssueDocumentId);
        builder.HasIndex(unit => unit.ReturnDocumentId);

        builder.ToTable("tracked_material_units", table =>
        {
            table.HasCheckConstraint("ck_tracked_units_holder_type_valid", "holder_type IN ('Employee', 'OrganizationalUnit', 'Site', 'External')");
            table.HasCheckConstraint("ck_tracked_units_kind_valid", "custody_kind IN ('Operational', 'Personal')");
            table.HasCheckConstraint("ck_tracked_units_personal_requires_employee", "custody_kind <> 'Personal' OR holder_type = 'Employee'");
            table.HasCheckConstraint("ck_tracked_units_operational_requires_non_employee", "custody_kind <> 'Operational' OR holder_type <> 'Employee'");
            table.HasCheckConstraint("ck_tracked_units_status_valid", "status IN ('Issued', 'Returned', 'Disposed')");
            table.HasCheckConstraint("ck_tracked_units_row_version_positive", "row_version > 0");
            table.HasCheckConstraint("ck_tracked_units_serial_nonempty", "length(trim(serial_number)) > 0");
        });

        builder.HasOne<Material>().WithMany().HasForeignKey(unit => unit.MaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(unit => unit.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WarehouseDocument>().WithMany().HasForeignKey(unit => unit.IssueDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WarehouseDocument>().WithMany().HasForeignKey(unit => unit.ReturnDocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}
