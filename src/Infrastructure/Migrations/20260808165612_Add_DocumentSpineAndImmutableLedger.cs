using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_DocumentSpineAndImmutableLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_balances",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    last_updated_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_balances", x => x.id);
                    table.CheckConstraint("ck_inventory_balances_quantity_non_negative", "quantity >= 0");
                    table.CheckConstraint("ck_inventory_balances_row_version_positive", "row_version > 0");
                    table.ForeignKey(
                        name: "fk_inventory_balances_materials_material_id",
                        column: x => x.material_id,
                        principalSchema: "public",
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_balances_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "public",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_attachments",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attachment_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    original_filename = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_attachments", x => x.id);
                    table.CheckConstraint("ck_document_attachments_attachment_type_valid", "attachment_type IN ('SignedOriginal', 'Supporting')");
                    table.CheckConstraint("ck_document_attachments_file_size_positive", "file_size > 0");
                    table.ForeignKey(
                        name: "fk_document_attachments_users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warehouse_documents",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    paper_document_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    paper_document_year = table.Column<int>(type: "integer", nullable: true),
                    system_reference_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    signed_copy_attachment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    document_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    posted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    posted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reversal_of_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_warehouse_documents", x => x.id);
                    table.CheckConstraint("ck_warehouse_documents_document_status_valid", "document_status IN ('Draft', 'Submitted', 'Posted', 'Reversed', 'Cancelled', 'Rejected')");
                    table.CheckConstraint("ck_warehouse_documents_document_type_valid", "document_type IN ('Receiving', 'Issue', 'Transfer', 'Adjustment', 'Opening', 'Return')");
                    table.CheckConstraint("ck_warehouse_documents_paper_document_year_valid", "paper_document_year IS NULL OR paper_document_year BETWEEN 1900 AND 9999");
                    table.CheckConstraint("ck_warehouse_documents_posted_metadata", "(document_status IN ('Posted', 'Reversed') AND posted_by IS NOT NULL AND posted_at_utc IS NOT NULL AND signed_copy_attachment_id IS NOT NULL) OR (document_status NOT IN ('Posted', 'Reversed') AND posted_by IS NULL AND posted_at_utc IS NULL)");
                    table.CheckConstraint("ck_warehouse_documents_row_version_positive", "row_version > 0");
                    table.ForeignKey(
                        name: "fk_warehouse_documents_document_attachments_signed_copy_attach",
                        column: x => x.signed_copy_attachment_id,
                        principalSchema: "public",
                        principalTable: "document_attachments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_warehouse_documents_users_created_by",
                        column: x => x.created_by,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_warehouse_documents_users_posted_by",
                        column: x => x.posted_by,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_warehouse_documents_warehouse_documents_reversal_of_documen",
                        column: x => x.reversal_of_document_id,
                        principalSchema: "public",
                        principalTable: "warehouse_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_warehouse_documents_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "public",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_lines",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    base_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    batch_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_lines", x => x.id);
                    table.CheckConstraint("ck_document_lines_base_quantity_positive", "base_quantity > 0");
                    table.CheckConstraint("ck_document_lines_line_type_valid", "line_type IN ('Normal', 'Asset')");
                    table.CheckConstraint("ck_document_lines_quantity_positive", "quantity > 0");
                    table.CheckConstraint("ck_document_lines_unit_price_non_negative", "unit_price >= 0");
                    table.ForeignKey(
                        name: "fk_document_lines_materials_material_id",
                        column: x => x.material_id,
                        principalSchema: "public",
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_lines_units_of_measure_unit_id",
                        column: x => x.unit_id,
                        principalSchema: "public",
                        principalTable: "units_of_measure",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_lines_warehouse_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "public",
                        principalTable: "warehouse_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    quantity_delta = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    posted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    posted_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_movements", x => x.id);
                    table.CheckConstraint("ck_stock_movements_movement_type_valid", "movement_type IN ('Receipt', 'Issue', 'TransferIn', 'TransferOut', 'AdjustmentIn', 'AdjustmentOut', 'Opening')");
                    table.CheckConstraint("ck_stock_movements_quantity_delta_not_zero", "quantity_delta <> 0");
                    table.ForeignKey(
                        name: "fk_stock_movements_document_lines_line_id",
                        column: x => x.line_id,
                        principalSchema: "public",
                        principalTable: "document_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movements_materials_material_id",
                        column: x => x.material_id,
                        principalSchema: "public",
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movements_users_posted_by",
                        column: x => x.posted_by,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movements_warehouse_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "public",
                        principalTable: "warehouse_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movements_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "public",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "permissions",
                columns: new[] { "id", "code", "description" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000115"), "warehouse-documents:view", "View warehouse documents, lines, attachments, and the ledger." },
                    { new Guid("00000000-0000-0000-0000-000000000116"), "warehouse-documents:create", "Create warehouse documents." },
                    { new Guid("00000000-0000-0000-0000-000000000117"), "warehouse-documents:edit", "Edit a Draft warehouse document: lines, paper reference, and attachments." },
                    { new Guid("00000000-0000-0000-0000-000000000118"), "warehouse-documents:submit", "Submit a Draft warehouse document for review." },
                    { new Guid("00000000-0000-0000-0000-000000000119"), "warehouse-documents:cancel", "Cancel a warehouse document before it is posted." },
                    { new Guid("00000000-0000-0000-0000-000000000120"), "warehouse-documents:review", "Post or reject a submitted warehouse document." },
                    { new Guid("00000000-0000-0000-0000-000000000121"), "warehouse-documents:reverse", "Authorize posting a reversal of a posted warehouse document." }
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000115"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000116"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000117"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000118"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000119"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000120"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000121"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000115"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("00000000-0000-0000-0000-000000000116"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("00000000-0000-0000-0000-000000000117"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("00000000-0000-0000-0000-000000000118"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("00000000-0000-0000-0000-000000000119"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("00000000-0000-0000-0000-000000000115"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000120"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000121"), new Guid("00000000-0000-0000-0000-000000000003") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_attachments_storage_key",
                schema: "public",
                table: "document_attachments",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_attachments_uploaded_by",
                schema: "public",
                table: "document_attachments",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "ux_document_attachments_signed_original",
                schema: "public",
                table: "document_attachments",
                columns: new[] { "document_id", "attachment_type" },
                unique: true,
                filter: "attachment_type = 'SignedOriginal'");

            migrationBuilder.CreateIndex(
                name: "ix_document_lines_document_id",
                schema: "public",
                table: "document_lines",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_lines_document_id_material_id",
                schema: "public",
                table: "document_lines",
                columns: new[] { "document_id", "material_id" });

            migrationBuilder.CreateIndex(
                name: "ix_document_lines_material_id",
                schema: "public",
                table: "document_lines",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_lines_unit_id",
                schema: "public",
                table: "document_lines",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_balances_material_id",
                schema: "public",
                table: "inventory_balances",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_balances_warehouse_id_material_id",
                schema: "public",
                table: "inventory_balances",
                columns: new[] { "warehouse_id", "material_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_document_id",
                schema: "public",
                table: "stock_movements",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_document_id_line_id_movement_type",
                schema: "public",
                table: "stock_movements",
                columns: new[] { "document_id", "line_id", "movement_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_line_id",
                schema: "public",
                table: "stock_movements",
                column: "line_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_material_id",
                schema: "public",
                table: "stock_movements",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_posted_by",
                schema: "public",
                table: "stock_movements",
                column: "posted_by");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_warehouse_id_material_id",
                schema: "public",
                table: "stock_movements",
                columns: new[] { "warehouse_id", "material_id" });

            migrationBuilder.CreateIndex(
                name: "ix_warehouse_documents_created_by",
                schema: "public",
                table: "warehouse_documents",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_warehouse_documents_posted_by",
                schema: "public",
                table: "warehouse_documents",
                column: "posted_by");

            migrationBuilder.CreateIndex(
                name: "ix_warehouse_documents_reversal_of_document_id",
                schema: "public",
                table: "warehouse_documents",
                column: "reversal_of_document_id",
                unique: true,
                filter: "reversal_of_document_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_warehouse_documents_signed_copy_attachment_id",
                schema: "public",
                table: "warehouse_documents",
                column: "signed_copy_attachment_id");

            migrationBuilder.CreateIndex(
                name: "ix_warehouse_documents_system_reference_number",
                schema: "public",
                table: "warehouse_documents",
                column: "system_reference_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_warehouse_documents_warehouse_id",
                schema: "public",
                table: "warehouse_documents",
                column: "warehouse_id");

            migrationBuilder.AddForeignKey(
                name: "fk_document_attachments_warehouse_documents_document_id",
                schema: "public",
                table: "document_attachments",
                column: "document_id",
                principalSchema: "public",
                principalTable: "warehouse_documents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // D-MOV-01: append-only ledger. A plain CHECK constraint cannot express "reject this
            // statement kind" - only a trigger can, so this is enforced here in addition to
            // StockMovement exposing no Update/Remove domain method.
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION public.trg_stock_movements_reject_mutation()
                RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'stock_movements is append-only: % is not allowed', TG_OP;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_stock_movements_append_only
                    BEFORE UPDATE OR DELETE ON public.stock_movements
                    FOR EACH ROW
                    EXECUTE FUNCTION public.trg_stock_movements_reject_mutation();
                """);

            // D-DOC-01, second layer (M3-PLAN.md §1.4): the CHECK constraint above only proves
            // signed_copy_attachment_id is set when Posted/Reversed - it cannot prove the
            // referenced row actually has attachment_type = 'SignedOriginal' and belongs to this
            // document, since that requires a cross-table lookup. This trigger closes that gap
            // independently of the application-layer gate in WarehouseDocument.MarkPosted.
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION public.trg_warehouse_documents_validate_signed_copy()
                RETURNS trigger AS $$
                BEGIN
                    IF NEW.document_status IN ('Posted', 'Reversed') THEN
                        IF NOT EXISTS (
                            SELECT 1 FROM public.document_attachments a
                            WHERE a.id = NEW.signed_copy_attachment_id
                              AND a.attachment_type = 'SignedOriginal'
                              AND a.document_id = NEW.id
                        ) THEN
                            RAISE EXCEPTION
                                'warehouse_documents % cannot become % without a SignedOriginal attachment',
                                NEW.id, NEW.document_status;
                        END IF;
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_warehouse_documents_signed_copy_required
                    BEFORE INSERT OR UPDATE ON public.warehouse_documents
                    FOR EACH ROW
                    EXECUTE FUNCTION public.trg_warehouse_documents_validate_signed_copy();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_warehouse_documents_signed_copy_required ON public.warehouse_documents;");

            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS public.trg_warehouse_documents_validate_signed_copy();");

            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_stock_movements_append_only ON public.stock_movements;");

            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS public.trg_stock_movements_reject_mutation();");

            migrationBuilder.DropForeignKey(
                name: "fk_document_attachments_warehouse_documents_document_id",
                schema: "public",
                table: "document_attachments");

            migrationBuilder.DropTable(
                name: "inventory_balances",
                schema: "public");

            migrationBuilder.DropTable(
                name: "stock_movements",
                schema: "public");

            migrationBuilder.DropTable(
                name: "document_lines",
                schema: "public");

            migrationBuilder.DropTable(
                name: "warehouse_documents",
                schema: "public");

            migrationBuilder.DropTable(
                name: "document_attachments",
                schema: "public");

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000115"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000116"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000117"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000118"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000119"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000120"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000121"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000115"), new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000116"), new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000117"), new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000118"), new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000119"), new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000115"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000120"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000121"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000115"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000116"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000117"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000118"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000119"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000120"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000121"));
        }
    }
}
