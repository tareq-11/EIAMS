using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Asset_Custody_Tracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "ak_document_lines_id_document_id",
                schema: "public",
                table: "document_lines",
                columns: new[] { "id", "document_id" });

            migrationBuilder.CreateTable(
                name: "asset_movement_history",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    moved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_movement_history", x => x.id);
                    table.CheckConstraint("ck_asset_movement_history_type_valid", "movement_type IN ('Received', 'Transferred', 'Issued', 'Returned', 'Disposed')");
                    table.ForeignKey(
                        name: "fk_asset_movement_history_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "public",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asset_movement_history_warehouse_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "public",
                        principalTable: "warehouse_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "custodies",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    holder_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    holder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    custody_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    issue_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custodies", x => x.id);
                    table.CheckConstraint("ck_custodies_holder_type_valid", "holder_type IN ('Employee', 'OrganizationalUnit', 'Site', 'External')");
                    table.CheckConstraint("ck_custodies_kind_valid", "custody_kind IN ('Operational', 'Personal')");
                    table.CheckConstraint("ck_custodies_row_version_positive", "row_version > 0");
                    table.CheckConstraint("ck_custodies_state_time_valid", "(status = 'Active' AND to_utc IS NULL AND return_document_id IS NULL) OR (status = 'Closed' AND to_utc IS NOT NULL AND from_utc < to_utc)");
                    table.CheckConstraint("ck_custodies_status_valid", "status IN ('Active', 'Closed')");
                    table.ForeignKey(
                        name: "fk_custodies_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "public",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_custodies_warehouse_documents_issue_document_id",
                        column: x => x.issue_document_id,
                        principalSchema: "public",
                        principalTable: "warehouse_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_custodies_warehouse_documents_return_document_id",
                        column: x => x.return_document_id,
                        principalSchema: "public",
                        principalTable: "warehouse_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_line_asset_selections",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_line_asset_selections", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_line_asset_selections_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "public",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_line_asset_selections_document_lines_document_line",
                        columns: x => new { x.document_line_id, x.document_id },
                        principalSchema: "public",
                        principalTable: "document_lines",
                        principalColumns: new[] { "id", "document_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_line_asset_selections_warehouse_documents_document",
                        column: x => x.document_id,
                        principalSchema: "public",
                        principalTable: "warehouse_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "return_info",
                schema: "public",
                columns: table => new
                {
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_issue_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_return_info", x => x.document_id);
                    table.CheckConstraint("ck_return_info_return_reason_not_blank", "length(btrim(return_reason)) > 0");
                    table.ForeignKey(
                        name: "fk_return_info_warehouse_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "public",
                        principalTable: "warehouse_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_return_info_warehouse_documents_original_issue_document_id",
                        column: x => x.original_issue_document_id,
                        principalSchema: "public",
                        principalTable: "warehouse_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "custody_history",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    custody_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    to_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custody_history", x => x.id);
                    table.CheckConstraint("ck_custody_history_actual_transition", "from_status <> to_status");
                    table.CheckConstraint("ck_custody_history_from_status_valid", "from_status IN ('Active', 'Closed')");
                    table.CheckConstraint("ck_custody_history_to_status_valid", "to_status IN ('Active', 'Closed')");
                    table.ForeignKey(
                        name: "fk_custody_history_custodies_custody_id",
                        column: x => x.custody_id,
                        principalSchema: "public",
                        principalTable: "custodies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_custody_history_users_changed_by",
                        column: x => x.changed_by,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_asset_movement_history_asset_id_document_id_movement_type",
                schema: "public",
                table: "asset_movement_history",
                columns: new[] { "asset_id", "document_id", "movement_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_movement_history_asset_id_moved_at_utc_id",
                schema: "public",
                table: "asset_movement_history",
                columns: new[] { "asset_id", "moved_at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_asset_movement_history_document_id",
                schema: "public",
                table: "asset_movement_history",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_custodies_asset_id",
                schema: "public",
                table: "custodies",
                column: "asset_id",
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_custodies_asset_id_status",
                schema: "public",
                table: "custodies",
                columns: new[] { "asset_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_custodies_holder_type_holder_id",
                schema: "public",
                table: "custodies",
                columns: new[] { "holder_type", "holder_id" });

            migrationBuilder.CreateIndex(
                name: "ix_custodies_issue_document_id",
                schema: "public",
                table: "custodies",
                column: "issue_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_custodies_return_document_id",
                schema: "public",
                table: "custodies",
                column: "return_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_custody_history_changed_by",
                schema: "public",
                table: "custody_history",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_custody_history_custody_id_at_utc_id",
                schema: "public",
                table: "custody_history",
                columns: new[] { "custody_id", "at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_document_line_asset_selections_asset_id",
                schema: "public",
                table: "document_line_asset_selections",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_line_asset_selections_document_id_asset_id",
                schema: "public",
                table: "document_line_asset_selections",
                columns: new[] { "document_id", "asset_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_line_asset_selections_document_line_id_asset_id",
                schema: "public",
                table: "document_line_asset_selections",
                columns: new[] { "document_line_id", "asset_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_line_asset_selections_document_line_id_document_id",
                schema: "public",
                table: "document_line_asset_selections",
                columns: new[] { "document_line_id", "document_id" });

            migrationBuilder.CreateIndex(
                name: "ix_return_info_original_issue_document_id",
                schema: "public",
                table: "return_info",
                column: "original_issue_document_id");

            migrationBuilder.Sql(
                """
                CREATE FUNCTION public.prevent_asset_movement_history_update()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION 'asset_movement_history rows are immutable';
                END;
                $function$;

                CREATE TRIGGER trg_asset_movement_history_prevent_update
                BEFORE UPDATE ON public.asset_movement_history
                FOR EACH ROW
                EXECUTE FUNCTION public.prevent_asset_movement_history_update();
                """);

            migrationBuilder.Sql(
                """
                CREATE VIEW public.v_asset_current_status AS
                SELECT
                    asset.id AS asset_id,
                    asset.material_id,
                    asset.warehouse_id,
                    asset.asset_number,
                    asset.serial_number,
                    CASE
                        WHEN latest_movement.movement_type = 'Disposed' THEN 'Disposed'
                        WHEN active_custody.custody_kind = 'Personal' THEN 'InCustody'
                        WHEN active_custody.custody_kind = 'Operational' THEN 'Issued'
                        WHEN latest_movement.movement_type IN ('Received', 'Returned') THEN 'InStock'
                        ELSE 'Unknown'
                    END AS current_status,
                    active_custody.id AS active_custody_id,
                    active_custody.holder_type,
                    active_custody.holder_id,
                    active_custody.custody_kind,
                    latest_movement.movement_type AS latest_movement_type,
                    latest_movement.moved_at_utc AS latest_movement_at_utc
                FROM public.assets AS asset
                LEFT JOIN LATERAL (
                    SELECT movement.movement_type, movement.moved_at_utc
                    FROM public.asset_movement_history AS movement
                    WHERE movement.asset_id = asset.id
                    ORDER BY movement.moved_at_utc DESC, movement.id DESC
                    LIMIT 1
                ) AS latest_movement ON TRUE
                LEFT JOIN LATERAL (
                    SELECT custody.id, custody.holder_type, custody.holder_id, custody.custody_kind
                    FROM public.custodies AS custody
                    WHERE custody.asset_id = asset.id AND custody.status = 'Active'
                    ORDER BY custody.from_utc DESC, custody.id DESC
                    LIMIT 1
                ) AS active_custody ON TRUE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS public.v_asset_current_status;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_asset_movement_history_prevent_update ON public.asset_movement_history;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS public.prevent_asset_movement_history_update();");

            migrationBuilder.DropTable(
                name: "asset_movement_history",
                schema: "public");

            migrationBuilder.DropTable(
                name: "custody_history",
                schema: "public");

            migrationBuilder.DropTable(
                name: "document_line_asset_selections",
                schema: "public");

            migrationBuilder.DropTable(
                name: "return_info",
                schema: "public");

            migrationBuilder.DropTable(
                name: "custodies",
                schema: "public");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_document_lines_id_document_id",
                schema: "public",
                table: "document_lines");
        }
    }
}
