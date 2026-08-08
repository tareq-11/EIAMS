using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_WarehousesCapabilitiesAndNumbering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_sequences",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    last_sequence = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_sequences", x => x.id);
                    table.CheckConstraint("ck_document_sequences_document_type_valid", "document_type IN ('Receiving', 'Issue', 'Transfer', 'Adjustment', 'Opening', 'Return')");
                    table.CheckConstraint("ck_document_sequences_last_sequence_non_negative", "last_sequence >= 0");
                    table.CheckConstraint("ck_document_sequences_year_valid", "year >= 2000");
                    table.ForeignKey(
                        name: "fk_document_sequences_sites_site_id",
                        column: x => x.site_id,
                        principalSchema: "public",
                        principalTable: "sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    warehouse_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    can_hold_stock = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_warehouses", x => x.id);
                    table.CheckConstraint("ck_warehouses_row_version_positive", "row_version > 0");
                    table.CheckConstraint("ck_warehouses_status_valid", "status IN ('Active', 'Inactive')");
                    table.ForeignKey(
                        name: "fk_warehouses_sites_site_id",
                        column: x => x.site_id,
                        principalSchema: "public",
                        principalTable: "sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warehouse_capabilities",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_domain_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_warehouse_capabilities", x => x.id);
                    table.CheckConstraint("ck_warehouse_capabilities_status_valid", "status IN ('Active', 'Inactive')");
                    table.ForeignKey(
                        name: "fk_warehouse_capabilities_material_domains_material_domain_id",
                        column: x => x.material_domain_id,
                        principalSchema: "public",
                        principalTable: "material_domains",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_warehouse_capabilities_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "public",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warehouse_material_settings",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    min_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    max_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_warehouse_material_settings", x => x.id);
                    table.CheckConstraint("ck_warehouse_material_settings_max_non_negative", "max_quantity >= 0");
                    table.CheckConstraint("ck_warehouse_material_settings_min_le_max", "min_quantity <= max_quantity");
                    table.CheckConstraint("ck_warehouse_material_settings_min_non_negative", "min_quantity >= 0");
                    table.CheckConstraint("ck_warehouse_material_settings_status_valid", "status IN ('Active', 'Inactive')");
                    table.ForeignKey(
                        name: "fk_warehouse_material_settings_materials_material_id",
                        column: x => x.material_id,
                        principalSchema: "public",
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_warehouse_material_settings_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "public",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warehouse_capability_operations",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    capability_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_warehouse_capability_operations", x => x.id);
                    table.CheckConstraint("ck_warehouse_capability_operations_operation_type_valid", "operation_type IN ('Receiving', 'Issue', 'Transfer', 'Count', 'Return')");
                    table.ForeignKey(
                        name: "fk_warehouse_capability_operations_warehouse_capabilities_capa",
                        column: x => x.capability_id,
                        principalSchema: "public",
                        principalTable: "warehouse_capabilities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "permissions",
                columns: new[] { "id", "code", "description" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000112"), "warehouses:manage", "Create, update, and change the status of warehouses." },
                    { new Guid("00000000-0000-0000-0000-000000000113"), "warehouse-capabilities:manage", "Grant, revoke, and configure the operations of warehouse capabilities." },
                    { new Guid("00000000-0000-0000-0000-000000000114"), "warehouse-material-settings:manage", "Create, update, and change the status of warehouse material settings." }
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000112"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000113"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000114"), new Guid("00000000-0000-0000-0000-000000000001") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_sequences_site_id_document_type_year",
                schema: "public",
                table: "document_sequences",
                columns: new[] { "site_id", "document_type", "year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_warehouse_capabilities_material_domain_id",
                schema: "public",
                table: "warehouse_capabilities",
                column: "material_domain_id");

            migrationBuilder.CreateIndex(
                name: "ix_warehouse_capabilities_warehouse_id_material_domain_id",
                schema: "public",
                table: "warehouse_capabilities",
                columns: new[] { "warehouse_id", "material_domain_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_warehouse_capability_operations_capability_id_operation_type",
                schema: "public",
                table: "warehouse_capability_operations",
                columns: new[] { "capability_id", "operation_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_warehouse_material_settings_material_id",
                schema: "public",
                table: "warehouse_material_settings",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "ix_warehouse_material_settings_warehouse_id_material_id",
                schema: "public",
                table: "warehouse_material_settings",
                columns: new[] { "warehouse_id", "material_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_warehouses_code",
                schema: "public",
                table: "warehouses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_warehouses_site_id",
                schema: "public",
                table: "warehouses",
                column: "site_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_sequences",
                schema: "public");

            migrationBuilder.DropTable(
                name: "warehouse_capability_operations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "warehouse_material_settings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "warehouse_capabilities",
                schema: "public");

            migrationBuilder.DropTable(
                name: "warehouses",
                schema: "public");

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000112"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000113"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000114"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000112"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000113"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000114"));
        }
    }
}
