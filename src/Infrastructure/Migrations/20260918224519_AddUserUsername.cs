using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserUsername : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: add the column nullable so the migration succeeds on tables that already
            // contain rows. The PB-002 backfill in step 2 derives the canonical username from the
            // email local part so legacy rows remain usable after the migration.
            migrationBuilder.AddColumn<string>(
                name: "username",
                schema: "public",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE public.users
                SET username = LOWER(SPLIT_PART(email, '@', 1))
                WHERE username IS NULL OR username = '';
                """);

            // Step 3: enforce NOT NULL once every row has a derived username. Disambiguate
            // collisions by appending a numeric suffix so the unique index can be created safely.
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT id,
                           ROW_NUMBER() OVER (PARTITION BY username ORDER BY created_at_utc, id) AS rn
                    FROM public.users
                )
                UPDATE public.users u
                SET username = u.username || ranked.rn
                FROM ranked
                WHERE u.id = ranked.id AND ranked.rn > 1;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "username",
                schema: "public",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                schema: "public",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_username",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "username",
                schema: "public",
                table: "users");
        }
    }
}