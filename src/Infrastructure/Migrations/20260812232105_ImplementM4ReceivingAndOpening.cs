using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImplementM4ReceivingAndOpening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "opening_type",
                schema: "public",
                table: "document_lines",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "assets",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    receipt_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    asset_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    serial_number = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    acquisition_date = table.Column<DateOnly>(type: "date", nullable: false),
                    warranty_expiry = table.Column<DateOnly>(type: "date", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assets", x => x.id);
                    table.CheckConstraint("ck_assets_asset_number_not_blank", "length(btrim(asset_number)) > 0");
                    table.CheckConstraint("ck_assets_row_version_positive", "row_version > 0");
                    table.CheckConstraint("ck_assets_warranty_after_acquisition", "warranty_expiry IS NULL OR warranty_expiry >= acquisition_date");
                    table.ForeignKey(
                        name: "fk_assets_document_lines_receipt_line_id",
                        column: x => x.receipt_line_id,
                        principalSchema: "public",
                        principalTable: "document_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assets_materials_material_id",
                        column: x => x.material_id,
                        principalSchema: "public",
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assets_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "public",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receiving_info",
                schema: "public",
                columns: table => new
                {
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supplier_invoice_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    receiving_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receiving_info", x => x.document_id);
                    table.CheckConstraint("ck_receiving_info_receiving_type_valid", "receiving_type IN ('Supplier', 'Transfer', 'Return')");
                    table.CheckConstraint("ck_receiving_info_supplier_ref_not_blank", "length(btrim(supplier_ref)) > 0");
                    table.ForeignKey(
                        name: "fk_receiving_info_warehouse_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "public",
                        principalTable: "warehouse_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_initial_opening_once",
                schema: "public",
                table: "stock_movements",
                columns: new[] { "warehouse_id", "material_id" },
                unique: true,
                filter: "movement_type = 'Opening' AND quantity_delta > 0");

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION public.enforce_receiving_info_document_type()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM public.warehouse_documents d
                        WHERE d.id = NEW.document_id
                          AND d.document_type = 'Receiving'
                    ) THEN
                        RAISE EXCEPTION 'ReceivingInfo can only belong to a Receiving document.'
                            USING ERRCODE = '23514',
                                  CONSTRAINT = 'ck_receiving_info_document_type';
                    END IF;

                    RETURN NEW;
                END;
                $function$;

                CREATE CONSTRAINT TRIGGER trg_receiving_info_document_type
                AFTER INSERT OR UPDATE OF document_id ON public.receiving_info
                DEFERRABLE INITIALLY IMMEDIATE
                FOR EACH ROW
                EXECUTE FUNCTION public.enforce_receiving_info_document_type();
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_document_lines_opening_type_valid",
                schema: "public",
                table: "document_lines",
                sql: "opening_type IS NULL OR opening_type IN ('Initial', 'Correction')");

            migrationBuilder.CreateIndex(
                name: "ix_assets_asset_number",
                schema: "public",
                table: "assets",
                column: "asset_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assets_material_id",
                schema: "public",
                table: "assets",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "ix_assets_receipt_line_id",
                schema: "public",
                table: "assets",
                column: "receipt_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_assets_warehouse_id_material_id",
                schema: "public",
                table: "assets",
                columns: new[] { "warehouse_id", "material_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_receiving_info_document_type ON public.receiving_info;
                DROP FUNCTION IF EXISTS public.enforce_receiving_info_document_type();
                """);

            migrationBuilder.DropTable(
                name: "assets",
                schema: "public");

            migrationBuilder.DropTable(
                name: "receiving_info",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "ix_stock_movements_initial_opening_once",
                schema: "public",
                table: "stock_movements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_document_lines_opening_type_valid",
                schema: "public",
                table: "document_lines");

            migrationBuilder.DropColumn(
                name: "opening_type",
                schema: "public",
                table: "document_lines");

        }
    }
}
