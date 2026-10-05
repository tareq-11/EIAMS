using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue 1, not the scaffolded 0: these roles were never version-tracked, so
            // 1 is their true first version. Defaulting to 0 would leave every role that is not
            // one of the four seeded rows failing ck_roles_row_version_positive, and the
            // AddCheckConstraint below would abort the whole migration on any database that
            // holds a custom role.
            migrationBuilder.AddColumn<int>(
                name: "row_version",
                schema: "public",
                table: "roles",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "row_version",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "row_version",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "row_version",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "row_version",
                value: 1);

            migrationBuilder.AddCheckConstraint(
                name: "ck_roles_row_version_positive",
                schema: "public",
                table: "roles",
                sql: "row_version > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_roles_row_version_positive",
                schema: "public",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "public",
                table: "roles");
        }
    }
}
