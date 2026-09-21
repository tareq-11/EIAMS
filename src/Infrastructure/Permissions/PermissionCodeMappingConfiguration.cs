using Domain.Permissions;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Permissions;

internal sealed class PermissionCodeMappingConfiguration : IEntityTypeConfiguration<PermissionCodeMapping>
{
    public void Configure(EntityTypeBuilder<PermissionCodeMapping> builder)
    {
        builder.ToTable("permission_code_mappings");
        builder.HasKey(mapping => mapping.Id);
        builder.Property(mapping => mapping.OldCode).HasMaxLength(100).IsRequired();
        builder.Property(mapping => mapping.NewCode).HasMaxLength(100).IsRequired();
        builder.Property(mapping => mapping.MappingVersion).IsRequired();
        builder.Property(mapping => mapping.Approver).HasMaxLength(200).IsRequired();
        builder.Property(mapping => mapping.ApprovedAtUtc).IsRequired();
        builder.Property(mapping => mapping.Rationale).HasMaxLength(2000).IsRequired();
        builder.Property(mapping => mapping.CreatedAtUtc).IsRequired();
        builder.ToTable(tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "ck_permission_code_mappings_mapping_version_positive",
                "mapping_version > 0");
            tableBuilder.HasCheckConstraint(
                "ck_permission_code_mappings_codes_non_empty",
                "length(trim(old_code)) > 0 AND length(trim(new_code)) > 0 AND length(trim(rationale)) > 0");
        });
        builder.HasIndex(mapping => new { mapping.OldCode, mapping.NewCode, mapping.MappingVersion })
            .IsUnique()
            .HasDatabaseName("ux_permission_code_mappings_old_new_version");
        builder.HasIndex(mapping => new { mapping.MappingVersion, mapping.OldCode });
        builder.HasIndex(mapping => new { mapping.MappingVersion, mapping.NewCode });

        DateTime approvedAt = new(2026, 9, 13, 0, 0, 0, DateTimeKind.Utc);
        builder.HasData(PermissionCodeMappingSeed.Rows(approvedAt));
    }
}

internal static class PermissionCodeMappingSeed
{
    private static Guid Id(int number) => Guid.Parse($"00000000-0000-0000-0000-00000000{number:0000}");

    public static object[] Rows(DateTime approvedAt)
    {
        return
        [
        Row(1, "assets:view", "asset.view", approvedAt),
        Row(2, "audit-logs:view", "audit.view", approvedAt),
        Row(3, "custody:view", "asset.view", approvedAt),
        Row(4, "custody:manage", "custody.assign", approvedAt),
        Row(5, "organizations:view", "organization.view", approvedAt),
        Row(6, "sites:view", "organization.view", approvedAt),
        Row(7, "org-units:view", "organization.view", approvedAt),
        Row(8, "employees:view", "organization.view", approvedAt),
        Row(9, "organizations:manage", "organization.manage", approvedAt),
        Row(10, "sites:manage", "organization.manage", approvedAt),
        Row(11, "org-units:manage", "organization.manage", approvedAt),
        Row(12, "employees:manage", "organization.manage", approvedAt),
        Row(13, "roles:view", "admin.role.view", approvedAt),
        Row(14, "roles:manage", "admin.role.manage", approvedAt),
        Row(15, "material-categories:manage", "catalog.manage", approvedAt),
        Row(16, "material-domains:manage", "catalog.manage", approvedAt),
        Row(17, "material-families:manage", "catalog.manage", approvedAt),
        Row(18, "materials:manage", "catalog.manage", approvedAt),
        Row(19, "units-of-measure:manage", "catalog.manage", approvedAt),
        Row(20, "materials:view", "catalog.view", approvedAt),
        Row(21, "units-of-measure:view", "catalog.view", approvedAt),
        Row(22, "warehouse-capabilities:manage", "warehouse.manage", approvedAt),
        Row(23, "warehouse-material-settings:manage", "warehouse.manage", approvedAt),
        Row(24, "warehouses:manage", "warehouse.manage", approvedAt),
        Row(25, "warehouses:view", "warehouse.view", approvedAt),
        Row(26, "inventory:view", "inventory.view", approvedAt),
        Row(27, "warehouse-documents:view", "document.view", approvedAt),
        Row(28, "warehouse-documents:create", "document.create", approvedAt),
        Row(29, "warehouse-documents:edit", "document.update", approvedAt),
        Row(30, "warehouse-documents:submit", "document.submit", approvedAt),
        Row(31, "warehouse-documents:cancel", "document.cancel", approvedAt),
        Row(32, "warehouse-documents:review", "document.post", approvedAt),
        Row(33, "warehouse-documents:review", "document.reject", approvedAt),
        Row(34, "warehouse-documents:reverse", "document.reverse", approvedAt),
        Row(35, "inventory-counts:view", "count.view", approvedAt),
        Row(36, "inventory-counts:plan", "count.plan", approvedAt),
        Row(37, "inventory-counts:enter-actual", "count.enter", approvedAt),
        Row(38, "inventory-counts:review", "count.plan", approvedAt),
        Row(39, "inventory-counts:review", "count.complete", approvedAt),
        Row(40, "inventory-counts:review", "count.close", approvedAt),
        Row(41, "users:access", "admin.user.view", approvedAt),
        Row(42, "users:access", "admin.user.manage", approvedAt)
        ];
    }

    private static object Row(int number, string oldCode, string newCode, DateTime approvedAt)
    {
        return new
        {
            Id = Id(number),
            OldCode = oldCode,
            NewCode = newCode,
            MappingVersion = 1,
            Approver = "human-owner-2026-09-13",
            ApprovedAtUtc = approvedAt,
            Rationale = RationaleFor(oldCode, newCode),
            CreatedAtUtc = approvedAt
        };
    }

    private static string RationaleFor(string oldCode, string newCode) => (oldCode, newCode) switch
    {
        ("assets:view", "asset.view") => "Asset registry, movement, and custody-read intent.",
        ("audit-logs:view", "audit.view") => "Audit-read intent; recipient policy limits the grant to AUDITOR.",
        ("custody:view", "asset.view") => "Asset read includes custody timeline and derived status.",
        ("custody:manage", "custody.assign") => "Scoped custody assignment and transfer operation.",
        (_, "organization.view") => "Consolidated organizational reference read.",
        (_, "organization.manage") => "Consolidated structural organizational administration.",
        ("roles:view", "admin.role.view") => "Role and permission-catalog read.",
        ("roles:manage", "admin.role.manage") => "Role and role-permission administration.",
        (_, "catalog.manage") => "Consolidated master-catalog management; target manage includes its read capability.",
        (_, "catalog.view") => "Consolidated catalog read.",
        (_, "warehouse.manage") => "Consolidated warehouse structure, capability, and settings management.",
        ("warehouses:view", "warehouse.view") => "Warehouse, capability, and settings read.",
        ("inventory:view", "inventory.view") => "Balances and movement-ledger read intent.",
        ("warehouse-documents:view", "document.view") => "Shared document read including policy, history, and attachments.",
        ("warehouse-documents:create", "document.create") => "Start a document draft in its warehouse.",
        ("warehouse-documents:edit", "document.update") => "Change draft content and draft attachments.",
        ("warehouse-documents:submit", "document.submit") => "Draft to submitted lifecycle transition.",
        ("warehouse-documents:cancel", "document.cancel") => "Pre-post cancellation with actor and state restrictions.",
        ("warehouse-documents:review", "document.post") => "Submitted warehouse document posting review action.",
        ("warehouse-documents:review", "document.reject") => "Submitted warehouse document rejection review action; not revise.",
        ("warehouse-documents:reverse", "document.reverse") => "Governed compensating reversal.",
        ("inventory-counts:view", "count.view") => "Count, session, and variance read.",
        ("inventory-counts:plan", "count.plan") => "Count planning and creation.",
        ("inventory-counts:enter-actual", "count.enter") => "Keeper entry of count actuals.",
        ("inventory-counts:review", "count.plan") => "Broad review maps Start to count plan because v1 has no count.start.",
        ("inventory-counts:review", "count.complete") => "Manager count completion lifecycle action.",
        ("inventory-counts:review", "count.close") => "Manager count closure lifecycle action.",
        ("users:access", "admin.user.view") => "Existing user list and detail administration read.",
        ("users:access", "admin.user.manage") => "Existing user create and update administration.",
        _ => throw new InvalidOperationException($"No semantic rationale for {oldCode} -> {newCode}.")
    };
}
