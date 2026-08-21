using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Inventory_Count_Adjustment_And_Disposal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_custodies_state_time_valid",
                schema: "public",
                table: "custodies");

            migrationBuilder.AddColumn<Guid>(
                name: "disposal_document_id",
                schema: "public",
                table: "custodies",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "inventory_counts",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    count_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scope_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    scope_material_domain_id = table.Column<Guid>(type: "uuid", nullable: true),
                    freeze_policy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    planned_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_counts", x => x.id);
                    table.CheckConstraint("ck_inventory_counts_freeze_valid", "freeze_policy IN ('HardFreeze', 'SoftFreeze', 'NoFreeze')");
                    table.CheckConstraint("ck_inventory_counts_row_version_positive", "row_version > 0");
                    table.CheckConstraint("ck_inventory_counts_scope_reference", "(scope_type = 'MaterialDomain' AND scope_material_domain_id IS NOT NULL) OR (scope_type <> 'MaterialDomain' AND scope_material_domain_id IS NULL)");
                    table.CheckConstraint("ck_inventory_counts_scope_valid", "scope_type IN ('EntireWarehouse', 'MaterialDomain', 'SelectedMaterials')");
                    table.CheckConstraint("ck_inventory_counts_status_valid", "status IN ('Planned', 'InProgress', 'Completed', 'Closed')");
                    table.CheckConstraint("ck_inventory_counts_timestamps", "(started_at_utc IS NULL OR started_at_utc >= planned_at_utc) AND (completed_at_utc IS NULL OR (started_at_utc IS NOT NULL AND completed_at_utc >= started_at_utc)) AND (closed_at_utc IS NULL OR (completed_at_utc IS NOT NULL AND closed_at_utc >= completed_at_utc))");
                    table.CheckConstraint("ck_inventory_counts_type_valid", "count_type IN ('Scheduled', 'Surprise', 'Cycle')");
                    table.ForeignKey(
                        name: "fk_inventory_counts_material_domains_scope_material_domain_id",
                        column: x => x.scope_material_domain_id,
                        principalSchema: "public",
                        principalTable: "material_domains",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_counts_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_counts_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "public",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_adjustments",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    count_id = table.Column<Guid>(type: "uuid", nullable: true),
                    adjustment_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_adjustments", x => x.id);
                    table.CheckConstraint("ck_inventory_adjustments_kind_valid", "adjustment_kind IN ('Quantity', 'Disposal')");
                    table.CheckConstraint("ck_inventory_adjustments_reason_not_blank", "length(btrim(reason)) > 0");
                    table.CheckConstraint("ck_inventory_adjustments_status_valid", "status IN ('Draft', 'Posted', 'Reversed')");
                    table.ForeignKey(
                        name: "fk_inventory_adjustments_inventory_counts_count_id",
                        column: x => x.count_id,
                        principalSchema: "public",
                        principalTable: "inventory_counts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_adjustments_warehouse_documents_id",
                        column: x => x.id,
                        principalSchema: "public",
                        principalTable: "warehouse_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_count_lines",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    count_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                    snapshot_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    actual_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    difference = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    variance_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_count_lines", x => x.id);
                    table.CheckConstraint("ck_inventory_count_lines_actual_difference", "(actual_quantity IS NULL AND difference IS NULL) OR (actual_quantity IS NOT NULL AND difference = actual_quantity - snapshot_quantity)");
                    table.CheckConstraint("ck_inventory_count_lines_actual_nonnegative", "actual_quantity IS NULL OR actual_quantity >= 0");
                    table.CheckConstraint("ck_inventory_count_lines_asset_quantities", "asset_id IS NULL OR (snapshot_quantity IN (0, 1) AND (actual_quantity IS NULL OR actual_quantity IN (0, 1)))");
                    table.CheckConstraint("ck_inventory_count_lines_snapshot_nonnegative", "snapshot_quantity >= 0");
                    table.ForeignKey(
                        name: "fk_inventory_count_lines_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "public",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_count_lines_inventory_counts_count_id",
                        column: x => x.count_id,
                        principalSchema: "public",
                        principalTable: "inventory_counts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_inventory_count_lines_materials_material_id",
                        column: x => x.material_id,
                        principalSchema: "public",
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_count_scope_materials",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    count_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_count_scope_materials", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventory_count_scope_materials_inventory_counts_count_id",
                        column: x => x.count_id,
                        principalSchema: "public",
                        principalTable: "inventory_counts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_inventory_count_scope_materials_materials_material_id",
                        column: x => x.material_id,
                        principalSchema: "public",
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "adjustment_lines",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    adjustment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    difference = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_adjustment_lines", x => x.id);
                    table.CheckConstraint("ck_adjustment_lines_difference_precision", "difference BETWEEN -999999999999999.999 AND 999999999999999.999");
                    table.CheckConstraint("ck_adjustment_lines_reason_not_blank", "length(btrim(reason)) > 0");
                    table.ForeignKey(
                        name: "fk_adjustment_lines_document_lines_id_adjustment_id",
                        columns: x => new { x.id, x.adjustment_id },
                        principalSchema: "public",
                        principalTable: "document_lines",
                        principalColumns: new[] { "id", "document_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_adjustment_lines_inventory_adjustments_adjustment_id",
                        column: x => x.adjustment_id,
                        principalSchema: "public",
                        principalTable: "inventory_adjustments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_custodies_disposal_document_id",
                schema: "public",
                table: "custodies",
                column: "disposal_document_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_custodies_state_time_valid",
                schema: "public",
                table: "custodies",
                sql: "(status = 'Active' AND to_utc IS NULL AND return_document_id IS NULL AND disposal_document_id IS NULL) OR (status = 'Closed' AND to_utc IS NOT NULL AND from_utc < to_utc AND NOT (return_document_id IS NOT NULL AND disposal_document_id IS NOT NULL))");

            migrationBuilder.CreateIndex(
                name: "ix_adjustment_lines_adjustment_id",
                schema: "public",
                table: "adjustment_lines",
                column: "adjustment_id");

            migrationBuilder.CreateIndex(
                name: "ix_adjustment_lines_id_adjustment_id",
                schema: "public",
                table: "adjustment_lines",
                columns: new[] { "id", "adjustment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inventory_adjustments_count_id",
                schema: "public",
                table: "inventory_adjustments",
                column: "count_id",
                unique: true,
                filter: "count_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_count_lines_asset_id",
                schema: "public",
                table: "inventory_count_lines",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_count_lines_count_id_asset_id",
                schema: "public",
                table: "inventory_count_lines",
                columns: new[] { "count_id", "asset_id" },
                unique: true,
                filter: "asset_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_count_lines_count_id_material_id",
                schema: "public",
                table: "inventory_count_lines",
                columns: new[] { "count_id", "material_id" },
                unique: true,
                filter: "asset_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_count_lines_material_id",
                schema: "public",
                table: "inventory_count_lines",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_count_scope_materials_count_id_material_id",
                schema: "public",
                table: "inventory_count_scope_materials",
                columns: new[] { "count_id", "material_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inventory_count_scope_materials_material_id",
                schema: "public",
                table: "inventory_count_scope_materials",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_counts_created_by_user_id",
                schema: "public",
                table: "inventory_counts",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_counts_scope_material_domain_id",
                schema: "public",
                table: "inventory_counts",
                column: "scope_material_domain_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_counts_warehouse_id",
                schema: "public",
                table: "inventory_counts",
                column: "warehouse_id",
                unique: true,
                filter: "status = 'InProgress'");

            migrationBuilder.AddForeignKey(
                name: "fk_custodies_warehouse_documents_disposal_document_id",
                schema: "public",
                table: "custodies",
                column: "disposal_document_id",
                principalSchema: "public",
                principalTable: "warehouse_documents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_custodies_warehouse_documents_disposal_document_id",
                schema: "public",
                table: "custodies");

            migrationBuilder.DropTable(
                name: "adjustment_lines",
                schema: "public");

            migrationBuilder.DropTable(
                name: "inventory_count_lines",
                schema: "public");

            migrationBuilder.DropTable(
                name: "inventory_count_scope_materials",
                schema: "public");

            migrationBuilder.DropTable(
                name: "inventory_adjustments",
                schema: "public");

            migrationBuilder.DropTable(
                name: "inventory_counts",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "ix_custodies_disposal_document_id",
                schema: "public",
                table: "custodies");

            migrationBuilder.DropCheckConstraint(
                name: "ck_custodies_state_time_valid",
                schema: "public",
                table: "custodies");

            migrationBuilder.DropColumn(
                name: "disposal_document_id",
                schema: "public",
                table: "custodies");

            migrationBuilder.AddCheckConstraint(
                name: "ck_custodies_state_time_valid",
                schema: "public",
                table: "custodies",
                sql: "(status = 'Active' AND to_utc IS NULL AND return_document_id IS NULL) OR (status = 'Closed' AND to_utc IS NOT NULL AND from_utc < to_utc)");
        }
    }
}
