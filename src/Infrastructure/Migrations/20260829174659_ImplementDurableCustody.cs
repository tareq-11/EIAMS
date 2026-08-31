using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImplementDurableCustody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "durable_custody_allocations",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    holder_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    holder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    custody_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    issue_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    active_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    returned_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_durable_custody_allocations", x => x.id);
                    table.CheckConstraint("ck_durable_alloc_holder_type_valid", "holder_type IN ('Employee', 'OrganizationalUnit', 'Site', 'External')");
                    table.CheckConstraint("ck_durable_alloc_kind_valid", "custody_kind IN ('Operational', 'Personal')");
                    table.CheckConstraint("ck_durable_alloc_operational_requires_non_employee", "custody_kind <> 'Operational' OR holder_type <> 'Employee'");
                    table.CheckConstraint("ck_durable_alloc_personal_requires_employee", "custody_kind <> 'Personal' OR holder_type = 'Employee'");
                    table.CheckConstraint("ck_durable_alloc_quantities", "issued_quantity > 0 AND active_quantity >= 0 AND returned_quantity >= 0 AND (active_quantity + returned_quantity = issued_quantity)");
                    table.CheckConstraint("ck_durable_alloc_row_version_positive", "row_version > 0");
                    table.CheckConstraint("ck_durable_alloc_status_valid", "status IN ('Active', 'FullyReturned')");
                    table.ForeignKey(
                        name: "fk_durable_custody_allocations_materials_material_id",
                        column: x => x.material_id,
                        principalSchema: "public",
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_durable_custody_allocations_warehouse_documents_issue_docum",
                        column: x => x.issue_document_id,
                        principalSchema: "public",
                        principalTable: "warehouse_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_durable_custody_allocations_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "public",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "durable_custody_histories",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    from_holder_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    from_holder_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_holder_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    to_holder_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    timestamp_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_durable_custody_histories", x => x.id);
                    table.CheckConstraint("ck_durable_history_action_valid", "action IN ('Issued', 'Returned', 'Transferred')");
                    table.CheckConstraint("ck_durable_history_subject_type", "subject_type IN ('TrackedUnit', 'MaterialQuantity')");
                    table.ForeignKey(
                        name: "fk_durable_custody_histories_users_actor_id",
                        column: x => x.actor_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_durable_custody_histories_warehouse_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "public",
                        principalTable: "warehouse_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tracked_material_units",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_tracked_material_units", x => x.id);
                    table.CheckConstraint("ck_tracked_units_holder_type_valid", "holder_type IN ('Employee', 'OrganizationalUnit', 'Site', 'External')");
                    table.CheckConstraint("ck_tracked_units_kind_valid", "custody_kind IN ('Operational', 'Personal')");
                    table.CheckConstraint("ck_tracked_units_operational_requires_non_employee", "custody_kind <> 'Operational' OR holder_type <> 'Employee'");
                    table.CheckConstraint("ck_tracked_units_personal_requires_employee", "custody_kind <> 'Personal' OR holder_type = 'Employee'");
                    table.CheckConstraint("ck_tracked_units_row_version_positive", "row_version > 0");
                    table.CheckConstraint("ck_tracked_units_serial_nonempty", "length(trim(serial_number)) > 0");
                    table.CheckConstraint("ck_tracked_units_status_valid", "status IN ('Issued', 'Returned', 'Disposed')");
                    table.ForeignKey(
                        name: "fk_tracked_material_units_materials_material_id",
                        column: x => x.material_id,
                        principalSchema: "public",
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tracked_material_units_warehouse_documents_issue_document_id",
                        column: x => x.issue_document_id,
                        principalSchema: "public",
                        principalTable: "warehouse_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tracked_material_units_warehouse_documents_return_document_",
                        column: x => x.return_document_id,
                        principalSchema: "public",
                        principalTable: "warehouse_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tracked_material_units_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "public",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_durable_custody_allocations_holder_type_holder_id",
                schema: "public",
                table: "durable_custody_allocations",
                columns: new[] { "holder_type", "holder_id" });

            migrationBuilder.CreateIndex(
                name: "ix_durable_custody_allocations_issue_document_id",
                schema: "public",
                table: "durable_custody_allocations",
                column: "issue_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_durable_custody_allocations_material_id",
                schema: "public",
                table: "durable_custody_allocations",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "ix_durable_custody_allocations_status",
                schema: "public",
                table: "durable_custody_allocations",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_durable_custody_allocations_warehouse_id",
                schema: "public",
                table: "durable_custody_allocations",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_durable_custody_histories_actor_id",
                schema: "public",
                table: "durable_custody_histories",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_durable_custody_histories_document_id",
                schema: "public",
                table: "durable_custody_histories",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_durable_custody_histories_subject_type_subject_id",
                schema: "public",
                table: "durable_custody_histories",
                columns: new[] { "subject_type", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ix_durable_custody_histories_timestamp_utc",
                schema: "public",
                table: "durable_custody_histories",
                column: "timestamp_utc");

            migrationBuilder.CreateIndex(
                name: "ix_tracked_material_units_holder_type_holder_id",
                schema: "public",
                table: "tracked_material_units",
                columns: new[] { "holder_type", "holder_id" });

            migrationBuilder.CreateIndex(
                name: "ix_tracked_material_units_issue_document_id",
                schema: "public",
                table: "tracked_material_units",
                column: "issue_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_tracked_material_units_return_document_id",
                schema: "public",
                table: "tracked_material_units",
                column: "return_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_tracked_material_units_warehouse_id",
                schema: "public",
                table: "tracked_material_units",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ux_tracked_material_units_active_serial",
                schema: "public",
                table: "tracked_material_units",
                columns: new[] { "material_id", "serial_number" },
                unique: true,
                filter: "status = 'Issued'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "durable_custody_allocations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "durable_custody_histories",
                schema: "public");

            migrationBuilder.DropTable(
                name: "tracked_material_units",
                schema: "public");
        }
    }
}
