using Infrastructure.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260901160000_AddTrigramSearchIndexes")]
public sealed class AddTrigramSearchIndexes : Migration
{
    private static readonly (string Name, string Definition)[] Indexes =
    [
        ("ix_materials_code_trgm", "public.materials USING gin (upper(code) gin_trgm_ops)"),
        ("ix_materials_name_ar_trgm", "public.materials USING gin (upper(name_ar) gin_trgm_ops)"),
        ("ix_warehouses_code_trgm", "public.warehouses USING gin (upper(code) gin_trgm_ops)"),
        ("ix_warehouses_name_trgm", "public.warehouses USING gin (upper(name) gin_trgm_ops)"),
        ("ix_assets_asset_number_trgm", "public.assets USING gin (upper(asset_number) gin_trgm_ops)"),
        ("ix_assets_serial_number_trgm", "public.assets USING gin (upper(serial_number) gin_trgm_ops) WHERE serial_number IS NOT NULL"),
        ("ix_warehouse_documents_system_reference_number_trgm", "public.warehouse_documents USING gin (upper(system_reference_number) gin_trgm_ops)"),
        ("ix_employees_full_name_trgm", "public.employees USING gin (full_name gin_trgm_ops)"),
        ("ix_employees_employee_number_trgm", "public.employees USING gin (employee_number gin_trgm_ops)"),
        ("ix_organizational_units_name_trgm", "public.organizational_units USING gin (name gin_trgm_ops)"),
        ("ix_sites_name_trgm", "public.sites USING gin (name gin_trgm_ops)"),
        ("ix_sites_code_trgm", "public.sites USING gin (code gin_trgm_ops)"),
        ("ix_external_parties_name_ar_trgm", "public.external_parties USING gin (name_ar gin_trgm_ops)"),
        ("ix_external_parties_code_trgm", "public.external_parties USING gin (code gin_trgm_ops) WHERE code IS NOT NULL"),
        ("ix_users_email_trgm", "public.users USING gin (upper(email) gin_trgm_ops)"),
        ("ix_users_first_name_trgm", "public.users USING gin (upper(first_name) gin_trgm_ops)"),
        ("ix_users_last_name_trgm", "public.users USING gin (upper(last_name) gin_trgm_ops)"),
        ("ix_inventory_adjustments_reason_trgm", "public.inventory_adjustments USING gin (upper(reason) gin_trgm_ops)"),
        ("ix_receiving_info_supplier_ref_trgm", "public.receiving_info USING gin (supplier_ref gin_trgm_ops)")
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

        foreach ((string name, string definition) in Indexes)
        {
            migrationBuilder.Sql(
                // Do not use IF NOT EXISTS here. If a cancelled concurrent build left an
                // invalid index with this name behind, PostgreSQL would otherwise skip it
                // and EF could record this migration as applied while the index is unusable.
                // Failing is intentional: the operator must inspect and recover the index
                // before retrying the migration.
                $"CREATE INDEX CONCURRENTLY {name} ON {definition};",
                suppressTransaction: true);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach ((string name, _) in Indexes.Reverse())
        {
            migrationBuilder.Sql(
                $"DROP INDEX CONCURRENTLY IF EXISTS public.{name};",
                suppressTransaction: true);
        }
    }
}
