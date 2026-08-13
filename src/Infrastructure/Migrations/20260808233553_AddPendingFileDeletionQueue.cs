using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingFileDeletionQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pending_file_deletion",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pending_file_deletion", x => x.id);
                    table.CheckConstraint("ck_pending_file_deletions_attempt_count_non_negative", "attempt_count >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_pending_file_deletion_next_attempt_at_utc",
                schema: "public",
                table: "pending_file_deletion",
                column: "next_attempt_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_pending_file_deletion_storage_key",
                schema: "public",
                table: "pending_file_deletion",
                column: "storage_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pending_file_deletion",
                schema: "public");
        }
    }
}
