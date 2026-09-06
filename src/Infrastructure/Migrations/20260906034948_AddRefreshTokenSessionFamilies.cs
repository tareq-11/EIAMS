using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenSessionFamilies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "session_id",
                schema: "public",
                table: "refresh_tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE public.refresh_tokens
                SET session_id = id
                WHERE session_id IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "session_id",
                schema: "public",
                table: "refresh_tokens",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id_session_id",
                schema: "public",
                table: "refresh_tokens",
                columns: new[] { "user_id", "session_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_user_id_session_id",
                schema: "public",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "session_id",
                schema: "public",
                table: "refresh_tokens");
        }
    }
}
