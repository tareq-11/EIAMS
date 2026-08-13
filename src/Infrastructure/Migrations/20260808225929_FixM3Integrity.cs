using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixM3Integrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_stock_movements_document_lines_line_id",
                schema: "public",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "ix_stock_movements_line_id",
                schema: "public",
                table: "stock_movements");

            migrationBuilder.AddColumn<Guid>(
                name: "source_line_id",
                schema: "public",
                table: "document_lines",
                type: "uuid",
                nullable: true);

            // Backfill reversal lines created before SourceLineId existed. Exact duplicate lines
            // are paired by a deterministic ordinal; because all business fields are identical,
            // either source line is equivalent for movement linkage.
            migrationBuilder.Sql(
                """
                WITH source_lines AS (
                    SELECT
                        reversal_document.id AS reversal_document_id,
                        source_line.id AS source_line_id,
                        source_line.material_id,
                        source_line.line_type,
                        source_line.quantity,
                        source_line.unit_id,
                        source_line.base_quantity,
                        source_line.unit_price,
                        source_line.batch_number,
                        source_line.expiry_date,
                        ROW_NUMBER() OVER (
                            PARTITION BY reversal_document.id, source_line.material_id,
                                source_line.line_type, source_line.quantity, source_line.unit_id,
                                source_line.base_quantity, source_line.unit_price,
                                source_line.batch_number, source_line.expiry_date
                            ORDER BY source_line.created_at_utc, source_line.id) AS match_number
                    FROM public.warehouse_documents reversal_document
                    JOIN public.document_lines source_line
                      ON source_line.document_id = reversal_document.reversal_of_document_id
                    WHERE reversal_document.reversal_of_document_id IS NOT NULL
                ),
                reversal_lines AS (
                    SELECT
                        reversal_line.id AS reversal_line_id,
                        reversal_line.document_id AS reversal_document_id,
                        reversal_line.material_id,
                        reversal_line.line_type,
                        reversal_line.quantity,
                        reversal_line.unit_id,
                        reversal_line.base_quantity,
                        reversal_line.unit_price,
                        reversal_line.batch_number,
                        reversal_line.expiry_date,
                        ROW_NUMBER() OVER (
                            PARTITION BY reversal_line.document_id, reversal_line.material_id,
                                reversal_line.line_type, reversal_line.quantity, reversal_line.unit_id,
                                reversal_line.base_quantity, reversal_line.unit_price,
                                reversal_line.batch_number, reversal_line.expiry_date
                            ORDER BY reversal_line.created_at_utc, reversal_line.id) AS match_number
                    FROM public.document_lines reversal_line
                    JOIN public.warehouse_documents reversal_document
                      ON reversal_document.id = reversal_line.document_id
                    WHERE reversal_document.reversal_of_document_id IS NOT NULL
                )
                UPDATE public.document_lines target
                   SET source_line_id = source.source_line_id
                  FROM reversal_lines reversal
                  JOIN source_lines source
                    ON source.reversal_document_id = reversal.reversal_document_id
                   AND source.material_id = reversal.material_id
                   AND source.line_type = reversal.line_type
                   AND source.quantity = reversal.quantity
                   AND source.unit_id IS NOT DISTINCT FROM reversal.unit_id
                   AND source.base_quantity = reversal.base_quantity
                   AND source.unit_price IS NOT DISTINCT FROM reversal.unit_price
                   AND source.batch_number IS NOT DISTINCT FROM reversal.batch_number
                   AND source.expiry_date IS NOT DISTINCT FROM reversal.expiry_date
                   AND source.match_number = reversal.match_number
                 WHERE target.id = reversal.reversal_line_id;
                """);

            // Stop with an actionable message instead of failing later with an opaque FK error.
            // This can only be hit by reversal rows created or manually edited before this fix.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                          FROM public.warehouse_documents reversal_document
                         WHERE reversal_document.reversal_of_document_id IS NOT NULL
                           AND (
                               (SELECT COUNT(*)
                                  FROM public.document_lines reversal_line
                                 WHERE reversal_line.document_id = reversal_document.id)
                               <>
                               (SELECT COUNT(*)
                                  FROM public.document_lines source_line
                                 WHERE source_line.document_id = reversal_document.reversal_of_document_id)
                               OR EXISTS (
                                   SELECT 1
                                     FROM public.document_lines reversal_line
                                    WHERE reversal_line.document_id = reversal_document.id
                                      AND reversal_line.source_line_id IS NULL
                               )
                           )
                    ) THEN
                        RAISE EXCEPTION USING
                            ERRCODE = '23514',
                            MESSAGE = 'FixM3Integrity cannot map one or more legacy reversal lines',
                            HINT = 'Repair or remove the invalid Draft reversal data, then apply the migration again.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                          FROM public.stock_movements movement
                          JOIN public.document_lines line ON line.id = movement.line_id
                         WHERE line.document_id <> movement.document_id
                            OR line.material_id <> movement.material_id
                    ) THEN
                        RAISE EXCEPTION USING
                            ERRCODE = '23514',
                            MESSAGE = 'FixM3Integrity found a legacy stock movement linked to the wrong document line',
                            HINT = 'Repair the legacy reversal movement linkage before applying this migration.';
                    END IF;
                END;
                $$;
                """);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_document_lines_id_document_id_material_id",
                schema: "public",
                table: "document_lines",
                columns: new[] { "id", "document_id", "material_id" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_line_id_document_id_material_id",
                schema: "public",
                table: "stock_movements",
                columns: new[] { "line_id", "document_id", "material_id" });

            migrationBuilder.CreateIndex(
                name: "ix_document_lines_source_line_id",
                schema: "public",
                table: "document_lines",
                column: "source_line_id",
                unique: true,
                filter: "source_line_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_document_lines_document_lines_source_line_id",
                schema: "public",
                table: "document_lines",
                column: "source_line_id",
                principalSchema: "public",
                principalTable: "document_lines",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_stock_movements_document_lines_line_id_document_id_material",
                schema: "public",
                table: "stock_movements",
                columns: new[] { "line_id", "document_id", "material_id" },
                principalSchema: "public",
                principalTable: "document_lines",
                principalColumns: new[] { "id", "document_id", "material_id" },
                onDelete: ReferentialAction.Restrict);

            // The posting transaction inserts movements before its final document status update.
            // A deferred constraint trigger validates the committed state without rejecting that
            // safe intermediate ordering.
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION public.trg_stock_movements_require_posted_document()
                RETURNS trigger AS $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                          FROM public.warehouse_documents document
                         WHERE document.id = NEW.document_id
                           AND document.document_status = 'Posted'
                    ) THEN
                        RAISE EXCEPTION
                            'stock movement % requires posted warehouse document %',
                            NEW.id, NEW.document_id
                            USING ERRCODE = '23514';
                    END IF;

                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql(
                """
                CREATE CONSTRAINT TRIGGER trg_stock_movements_posted_document
                    AFTER INSERT ON public.stock_movements
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW
                    EXECUTE FUNCTION public.trg_stock_movements_require_posted_document();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_stock_movements_posted_document ON public.stock_movements;");

            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS public.trg_stock_movements_require_posted_document();");

            migrationBuilder.DropForeignKey(
                name: "fk_document_lines_document_lines_source_line_id",
                schema: "public",
                table: "document_lines");

            migrationBuilder.DropForeignKey(
                name: "fk_stock_movements_document_lines_line_id_document_id_material",
                schema: "public",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "ix_stock_movements_line_id_document_id_material_id",
                schema: "public",
                table: "stock_movements");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_document_lines_id_document_id_material_id",
                schema: "public",
                table: "document_lines");

            migrationBuilder.DropIndex(
                name: "ix_document_lines_source_line_id",
                schema: "public",
                table: "document_lines");

            migrationBuilder.DropColumn(
                name: "source_line_id",
                schema: "public",
                table: "document_lines");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_line_id",
                schema: "public",
                table: "stock_movements",
                column: "line_id");

            migrationBuilder.AddForeignKey(
                name: "fk_stock_movements_document_lines_line_id",
                schema: "public",
                table: "stock_movements",
                column: "line_id",
                principalSchema: "public",
                principalTable: "document_lines",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
