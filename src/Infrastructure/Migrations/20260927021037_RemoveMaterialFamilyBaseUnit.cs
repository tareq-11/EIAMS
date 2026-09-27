using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMaterialFamilyBaseUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fail closed if material units or material-scoped conversion provenance are not
            // internally consistent. The family value is deliberately never used to backfill.
            migrationBuilder.Sql(
                @"DO $$
BEGIN
    -- Freeze catalog writes across validation and DDL so no bad unit/conversion can race the gate.
    LOCK TABLE public.material_families, public.materials,
               public.material_unit_conversions, public.units_of_measure
        IN SHARE ROW EXCLUSIVE MODE;

    IF EXISTS (
        SELECT 1
        FROM public.material_families AS f
        LEFT JOIN public.units_of_measure AS u ON u.id = f.base_unit_id
        WHERE f.base_unit_id IS NULL
           OR f.base_unit_id = '00000000-0000-0000-0000-000000000000'::uuid
           OR u.id IS NULL
    ) THEN
        RAISE EXCEPTION 'family-base-unit-preflight-failed';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM public.materials AS m
        LEFT JOIN public.units_of_measure AS u ON u.id = m.base_unit_id
        WHERE m.base_unit_id IS NULL
           OR m.base_unit_id = '00000000-0000-0000-0000-000000000000'::uuid
           OR u.id IS NULL
    ) THEN
        RAISE EXCEPTION 'material-base-unit-preflight-failed';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM public.material_unit_conversions AS c
        LEFT JOIN public.materials AS m ON m.id = c.material_id
        LEFT JOIN public.units_of_measure AS source_unit ON source_unit.id = c.from_unit_id
        LEFT JOIN public.units_of_measure AS target_unit ON target_unit.id = c.to_base_unit_id
        WHERE m.id IS NULL
           OR source_unit.id IS NULL
           OR target_unit.id IS NULL
           OR c.from_unit_id = c.to_base_unit_id
           OR c.to_base_unit_id <> m.base_unit_id
    ) THEN
        RAISE EXCEPTION 'material-conversion-provenance-preflight-failed';
    END IF;
END $$;");

            migrationBuilder.DropForeignKey(
                name: "fk_material_families_units_of_measure_base_unit_id",
                schema: "public",
                table: "material_families");

            migrationBuilder.DropIndex(
                name: "ix_material_families_base_unit_id",
                schema: "public",
                table: "material_families");

            migrationBuilder.DropColumn(
                name: "base_unit_id",
                schema: "public",
                table: "material_families");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "MaterialFamily.BaseUnitId was removed as a competing source of truth. " +
                "Restore a verified pre-migration database backup or implement a reviewed forward-fix; " +
                "the former family value cannot be reconstructed safely.");
        }
    }
}
