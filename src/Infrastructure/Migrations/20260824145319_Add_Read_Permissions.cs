using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Read_Permissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "public",
                table: "permissions",
                columns: new[] { "id", "code", "description" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000127"), "organizations:view", "View organizations." },
                    { new Guid("00000000-0000-0000-0000-000000000128"), "sites:view", "View sites." },
                    { new Guid("00000000-0000-0000-0000-000000000129"), "org-units:view", "View organizational units." },
                    { new Guid("00000000-0000-0000-0000-000000000130"), "employees:view", "View employees." },
                    { new Guid("00000000-0000-0000-0000-000000000131"), "roles:view", "View roles and permissions." },
                    { new Guid("00000000-0000-0000-0000-000000000132"), "units-of-measure:view", "View units of measure." },
                    { new Guid("00000000-0000-0000-0000-000000000133"), "materials:view", "View the material catalog and unit conversions." },
                    { new Guid("00000000-0000-0000-0000-000000000134"), "warehouses:view", "View warehouses, capabilities, and material settings." },
                    { new Guid("00000000-0000-0000-0000-000000000135"), "inventory:view", "View inventory balances and stock movements." },
                    { new Guid("00000000-0000-0000-0000-000000000136"), "assets:view", "View asset status." },
                    { new Guid("00000000-0000-0000-0000-000000000137"), "custody:view", "View custody state and history." },
                    { new Guid("00000000-0000-0000-0000-000000000138"), "custody:manage", "Assign and manage asset custody." }
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000127"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000128"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000129"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000130"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000131"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000132"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000133"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000134"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000135"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000136"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000137"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000138"), new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("00000000-0000-0000-0000-000000000133"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("00000000-0000-0000-0000-000000000134"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("00000000-0000-0000-0000-000000000135"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("00000000-0000-0000-0000-000000000136"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("00000000-0000-0000-0000-000000000137"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("00000000-0000-0000-0000-000000000138"), new Guid("00000000-0000-0000-0000-000000000002") },
                    { new Guid("00000000-0000-0000-0000-000000000133"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000134"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000135"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000136"), new Guid("00000000-0000-0000-0000-000000000003") },
                    { new Guid("00000000-0000-0000-0000-000000000137"), new Guid("00000000-0000-0000-0000-000000000003") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000127"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000128"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000129"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000130"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000131"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000132"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000133"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000134"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000135"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000136"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000137"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000138"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000133"), new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000134"), new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000135"), new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000136"), new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000137"), new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000138"), new Guid("00000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000133"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000134"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000135"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000136"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000137"), new Guid("00000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000127"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000128"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000129"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000130"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000131"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000132"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000133"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000134"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000135"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000136"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000137"));

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000138"));
        }
    }
}
