using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddMaterialConversionProvenance : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            @"DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM public.materials
        WHERE material_kind <> 'Asset' AND requires_asset_number = true
        LIMIT 1
    )
    THEN
        RAISE EXCEPTION 'contradictory-requires-asset-number';
    END IF;
END $$;

ALTER TABLE public.materials DROP COLUMN IF EXISTS requires_asset_number;
");

        migrationBuilder.AddColumn<int>(
            name: "catalog_version",
            schema: "public",
            table: "materials",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddCheckConstraint(
            name: "ck_materials_catalog_version_positive",
            schema: "public",
            table: "materials",
            sql: "catalog_version > 0");

        migrationBuilder.AddColumn<int>(
            name: "source_material_version",
            schema: "public",
            table: "document_lines",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "source_material_kind",
            schema: "public",
            table: "document_lines",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "source_tracking_type",
            schema: "public",
            table: "document_lines",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "source_base_unit_id",
            schema: "public",
            table: "document_lines",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "source_conversion_id",
            schema: "public",
            table: "document_lines",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "source_conversion_from_unit_id",
            schema: "public",
            table: "document_lines",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "source_conversion_to_unit_id",
            schema: "public",
            table: "document_lines",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "source_conversion_factor",
            schema: "public",
            table: "document_lines",
            type: "numeric(18,6)",
            precision: 18,
            scale: 6,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_document_lines_source_conversion_id",
            schema: "public",
            table: "document_lines",
            column: "source_conversion_id");

        migrationBuilder.CreateIndex(
            name: "ix_document_lines_document_id_source_material_version",
            schema: "public",
            table: "document_lines",
            columns: new[] { "document_id", "source_material_version" });

        migrationBuilder.AddCheckConstraint(
            name: "ck_document_lines_source_conversion_complete",
            schema: "public",
            table: "document_lines",
            sql: "(source_conversion_id IS NULL AND source_conversion_from_unit_id IS NULL AND source_conversion_to_unit_id IS NULL AND source_conversion_factor IS NULL) OR (source_conversion_id IS NOT NULL AND source_conversion_from_unit_id IS NOT NULL AND source_conversion_to_unit_id IS NOT NULL AND source_conversion_factor IS NOT NULL AND source_conversion_factor > 0)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_document_lines_source_conversion_required",
            schema: "public",
            table: "document_lines",
            sql: "source_material_version IS NULL OR source_base_unit_id IS NULL OR unit_id IS NULL OR unit_id = source_base_unit_id OR source_conversion_id IS NOT NULL");

        migrationBuilder.AddCheckConstraint(
            name: "ck_document_lines_source_material_version_positive",
            schema: "public",
            table: "document_lines",
            sql: "source_material_version IS NULL OR source_material_version > 0");

        migrationBuilder.AddCheckConstraint(
            name: "ck_document_lines_source_provenance_complete",
            schema: "public",
            table: "document_lines",
            sql: "(source_material_version IS NULL AND source_material_kind IS NULL AND source_tracking_type IS NULL AND source_base_unit_id IS NULL) OR (source_material_version IS NOT NULL AND source_material_version > 0 AND source_material_kind IS NOT NULL AND source_material_kind IN ('Consumable', 'Durable', 'Asset') AND source_tracking_type IS NOT NULL AND source_tracking_type IN ('Quantity', 'Serial') AND source_base_unit_id IS NOT NULL AND source_base_unit_id <> '00000000-0000-0000-0000-000000000000'::uuid)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_materials_catalog_version_positive",
            schema: "public",
            table: "materials");

        migrationBuilder.DropCheckConstraint(
            name: "ck_document_lines_source_provenance_complete",
            schema: "public",
            table: "document_lines");

        migrationBuilder.DropCheckConstraint(
            name: "ck_document_lines_source_material_version_positive",
            schema: "public",
            table: "document_lines");

        migrationBuilder.DropCheckConstraint(
            name: "ck_document_lines_source_conversion_required",
            schema: "public",
            table: "document_lines");

        migrationBuilder.DropCheckConstraint(
            name: "ck_document_lines_source_conversion_complete",
            schema: "public",
            table: "document_lines");

        migrationBuilder.DropIndex(
            name: "ix_document_lines_document_id_source_material_version",
            schema: "public",
            table: "document_lines");

        migrationBuilder.DropIndex(
            name: "ix_document_lines_source_conversion_id",
            schema: "public",
            table: "document_lines");

        migrationBuilder.DropColumn(
            name: "source_conversion_factor",
            schema: "public",
            table: "document_lines");

        migrationBuilder.DropColumn(
            name: "source_conversion_to_unit_id",
            schema: "public",
            table: "document_lines");

        migrationBuilder.DropColumn(
            name: "source_conversion_from_unit_id",
            schema: "public",
            table: "document_lines");

        migrationBuilder.DropColumn(
            name: "source_conversion_id",
            schema: "public",
            table: "document_lines");

        migrationBuilder.DropColumn(
            name: "source_base_unit_id",
            schema: "public",
            table: "document_lines");

        migrationBuilder.DropColumn(
            name: "source_tracking_type",
            schema: "public",
            table: "document_lines");

        migrationBuilder.DropColumn(
            name: "source_material_kind",
            schema: "public",
            table: "document_lines");

        migrationBuilder.DropColumn(
            name: "source_material_version",
            schema: "public",
            table: "document_lines");

        migrationBuilder.DropColumn(
            name: "catalog_version",
            schema: "public",
            table: "materials");

        migrationBuilder.AddColumn<bool>(
            name: "requires_asset_number",
            schema: "public",
            table: "materials",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql(
            @"UPDATE public.materials
SET requires_asset_number = (material_kind = 'Asset')");
    }
}
