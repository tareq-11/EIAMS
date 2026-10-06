using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue 1, NOT 0: the check constraint below requires a positive
            // version, so a 0 default would fail on every pre-existing row.
            migrationBuilder.AddColumn<int>(
                name: "row_version",
                schema: "public",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                """
                UPDATE "public"."users" SET row_version = 1 WHERE row_version <= 0;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_row_version_positive",
                schema: "public",
                table: "users",
                sql: "row_version > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_row_version_positive",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "public",
                table: "users");
        }
    }
}
