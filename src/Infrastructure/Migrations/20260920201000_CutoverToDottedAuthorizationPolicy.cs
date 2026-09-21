using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CutoverToDottedAuthorizationPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "public",
                table: "authorization_policy_versions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000901"),
                column: "is_active",
                value: false);

            migrationBuilder.InsertData(
                schema: "public",
                table: "authorization_policy_versions",
                columns: new[] { "id", "activated_at_utc", "activated_by", "active_vocabulary", "concurrency_token", "is_active", "mapping_version" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000902"), new DateTime(2026, 9, 20, 0, 0, 0, 0, DateTimeKind.Utc), "phase-5-cutover", "dotted-v1", 2L, true, 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "public",
                table: "authorization_policy_versions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000902"));

            migrationBuilder.UpdateData(
                schema: "public",
                table: "authorization_policy_versions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000901"),
                column: "is_active",
                value: true);
        }
    }
}
