using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleNameAr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "name_ar",
                schema: "public",
                table: "roles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // The seeded roles are corrected by the UpdateData statements below. A role an
            // administrator created before this migration receives the empty-string default
            // instead, which ck_roles_name_ar_not_blank and the role write validators both
            // reject, so it is backfilled here with the role code. That keeps every existing
            // row readable and non-blank until an administrator supplies a real Arabic label;
            // the role code is a placeholder, not a translated name.
            migrationBuilder.Sql("""
                UPDATE public.roles
                SET name_ar = name
                WHERE name_ar IS NULL OR btrim(name_ar) = '';
                """);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "name_ar",
                value: "مدير النظام");

            migrationBuilder.UpdateData(
                schema: "public",
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "name_ar",
                value: "أمين المستودع");

            migrationBuilder.UpdateData(
                schema: "public",
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "name_ar",
                value: "مدير المستودع");

            migrationBuilder.UpdateData(
                schema: "public",
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "name_ar",
                value: "مدقق");

            migrationBuilder.AddCheckConstraint(
                name: "ck_roles_name_ar_not_blank",
                schema: "public",
                table: "roles",
                sql: "length(btrim(name_ar)) > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_roles_name_ar_not_blank",
                schema: "public",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "name_ar",
                schema: "public",
                table: "roles");
        }
    }
}
