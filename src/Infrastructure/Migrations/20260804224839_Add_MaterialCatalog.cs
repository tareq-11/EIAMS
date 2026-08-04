using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_MaterialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "material_domains",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_material_domains", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "units_of_measure",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unit_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_units_of_measure", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "material_categories",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_domain_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_material_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_material_categories_material_categories_parent_category_id",
                        column: x => x.parent_category_id,
                        principalSchema: "public",
                        principalTable: "material_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_material_categories_material_domains_material_domain_id",
                        column: x => x.material_domain_id,
                        principalSchema: "public",
                        principalTable: "material_domains",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "material_families",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    base_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_material_families", x => x.id);
                    table.ForeignKey(
                        name: "fk_material_families_material_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "public",
                        principalTable: "material_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_material_families_units_of_measure_base_unit_id",
                        column: x => x.base_unit_id,
                        principalSchema: "public",
                        principalTable: "units_of_measure",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "materials",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    material_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tracking_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    has_expiry = table.Column<bool>(type: "boolean", nullable: false),
                    requires_asset_number = table.Column<bool>(type: "boolean", nullable: false),
                    attributes = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_materials", x => x.id);
                    table.ForeignKey(
                        name: "fk_materials_material_families_family_id",
                        column: x => x.family_id,
                        principalSchema: "public",
                        principalTable: "material_families",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "material_unit_conversions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_base_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    factor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_material_unit_conversions", x => x.id);
                    table.ForeignKey(
                        name: "fk_material_unit_conversions_materials_material_id",
                        column: x => x.material_id,
                        principalSchema: "public",
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_material_unit_conversions_units_of_measure_from_unit_id",
                        column: x => x.from_unit_id,
                        principalSchema: "public",
                        principalTable: "units_of_measure",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_material_unit_conversions_units_of_measure_to_base_unit_id",
                        column: x => x.to_base_unit_id,
                        principalSchema: "public",
                        principalTable: "units_of_measure",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "permissions",
                columns: new[] { "id", "code", "description" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000107"), "units-of-measure:manage", "Create and update units of measure." },
                    { new Guid("00000000-0000-0000-0000-000000000108"), "material-domains:manage", "Create, update, and change the status of material domains." },
                    { new Guid("00000000-0000-0000-0000-000000000109"), "material-categories:manage", "Create, update, and change the status of material categories." },
                    { new Guid("00000000-0000-0000-0000-000000000110"), "material-families:manage", "Create, update, and change the status of material families." },
                    { new Guid("00000000-0000-0000-0000-000000000111"), "materials:manage", "Create, update, and change the status of materials and their unit conversions." }
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000107"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000108"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000109"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000110"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000111"), new Guid("00000000-0000-0000-0000-000000000001") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_material_categories_material_domain_id",
                schema: "public",
                table: "material_categories",
                column: "material_domain_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_categories_parent_category_id",
                schema: "public",
                table: "material_categories",
                column: "parent_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_domains_code",
                schema: "public",
                table: "material_domains",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_material_families_base_unit_id",
                schema: "public",
                table: "material_families",
                column: "base_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_families_category_id",
                schema: "public",
                table: "material_families",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_unit_conversions_from_unit_id",
                schema: "public",
                table: "material_unit_conversions",
                column: "from_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_unit_conversions_material_id",
                schema: "public",
                table: "material_unit_conversions",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_unit_conversions_to_base_unit_id",
                schema: "public",
                table: "material_unit_conversions",
                column: "to_base_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_materials_code",
                schema: "public",
                table: "materials",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_materials_family_id",
                schema: "public",
                table: "materials",
                column: "family_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "material_unit_conversions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "materials",
                schema: "public");

            migrationBuilder.DropTable(
                name: "material_families",
                schema: "public");

            migrationBuilder.DropTable(
                name: "material_categories",
                schema: "public");

            migrationBuilder.DropTable(
                name: "units_of_measure",
                schema: "public");

            migrationBuilder.DropTable(
                name: "material_domains",
                schema: "public");

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000107"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000108"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000109"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000110"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000111"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000107"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000108"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000109"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000110"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000111"));
        }
    }
}
