using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionAllowedScopeTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "permission_allowed_scope_types",
                schema: "public",
                columns: table => new
                {
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permission_allowed_scope_types", x => new { x.permission_id, x.scope_type });
                    table.ForeignKey(
                        name: "fk_permission_allowed_scope_types_permissions_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "public",
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "permission_allowed_scope_types",
                columns: new[] { "permission_id", "scope_type" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000101"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000102"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000103"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000104"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000105"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000106"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000107"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000108"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000109"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000110"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000111"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000112"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000113"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000114"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000115"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000115"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000115"), "Site" },
                    { new Guid("00000000-0000-0000-0000-000000000115"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000116"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000117"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000118"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000119"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000120"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000121"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000122"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000122"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000122"), "Site" },
                    { new Guid("00000000-0000-0000-0000-000000000122"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000123"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000124"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000125"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000126"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000126"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000126"), "Site" },
                    { new Guid("00000000-0000-0000-0000-000000000126"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000127"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000127"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000127"), "Site" },
                    { new Guid("00000000-0000-0000-0000-000000000127"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000128"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000128"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000128"), "Site" },
                    { new Guid("00000000-0000-0000-0000-000000000128"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000129"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000129"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000129"), "Site" },
                    { new Guid("00000000-0000-0000-0000-000000000129"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000130"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000130"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000130"), "Site" },
                    { new Guid("00000000-0000-0000-0000-000000000130"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000131"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000132"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000132"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000132"), "Site" },
                    { new Guid("00000000-0000-0000-0000-000000000132"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000133"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000133"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000133"), "Site" },
                    { new Guid("00000000-0000-0000-0000-000000000133"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000134"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000134"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000134"), "Site" },
                    { new Guid("00000000-0000-0000-0000-000000000134"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000135"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000135"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000135"), "Site" },
                    { new Guid("00000000-0000-0000-0000-000000000135"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000136"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000136"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000136"), "Site" },
                    { new Guid("00000000-0000-0000-0000-000000000136"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000137"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000137"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000137"), "Site" },
                    { new Guid("00000000-0000-0000-0000-000000000137"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000138"), "Warehouse" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "permission_allowed_scope_types",
                schema: "public");
        }
    }
}
