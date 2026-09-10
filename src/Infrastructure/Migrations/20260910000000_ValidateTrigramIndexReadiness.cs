using Infrastructure.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Infrastructure.Migrations;

/// <summary>
/// Prevents a prior interrupted concurrent trigram-index build from being hidden by the
/// migrations history table. Recovery is deliberately an operator action; this migration never
/// drops or reindexes application indexes automatically.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260910000000_ValidateTrigramIndexReadiness")]
public sealed class ValidateTrigramIndexReadiness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                unhealthy_indexes text;
            BEGIN
                SELECT string_agg(format('%I.%I', 'public', expected.index_name), ', ' ORDER BY expected.index_name)
                INTO unhealthy_indexes
                FROM unnest(ARRAY[
                          'ix_materials_code_trgm', 'ix_materials_name_ar_trgm',
                          'ix_warehouses_code_trgm', 'ix_warehouses_name_trgm',
                          'ix_assets_asset_number_trgm', 'ix_assets_serial_number_trgm',
                          'ix_warehouse_documents_system_reference_number_trgm',
                          'ix_employees_full_name_trgm', 'ix_employees_employee_number_trgm',
                          'ix_organizational_units_name_trgm', 'ix_sites_name_trgm',
                          'ix_sites_code_trgm', 'ix_external_parties_name_ar_trgm',
                          'ix_external_parties_code_trgm', 'ix_users_email_trgm',
                          'ix_users_first_name_trgm', 'ix_users_last_name_trgm',
                          'ix_inventory_adjustments_reason_trgm', 'ix_receiving_info_supplier_ref_trgm'
                      ]::text[]) AS expected(index_name)
                LEFT JOIN pg_catalog.pg_class AS c
                  ON c.relname = expected.index_name
                 AND c.relnamespace = 'public'::regnamespace
                LEFT JOIN pg_catalog.pg_index AS i ON i.indexrelid = c.oid
                WHERE c.oid IS NULL
                   OR NOT i.indisvalid
                   OR NOT i.indisready;

                IF unhealthy_indexes IS NOT NULL THEN
                    RAISE EXCEPTION
                        'Missing, invalid, or not-ready concurrent index detected: %. Run scripts/migration-preflight.sql, follow the recovery runbook, and retry. No automatic DROP/REINDEX is performed.',
                        unhealthy_indexes;
                END IF;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Validation-only migration; it owns no database object.
    }
}
