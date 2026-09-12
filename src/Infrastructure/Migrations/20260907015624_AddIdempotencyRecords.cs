using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "idempotency_records",
                schema: "public",
                columns: table => new
                {
                    key = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    response_payload = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_records", x => x.key);
                    table.CheckConstraint("ck_idempotency_records_expiry", "expires_at_utc > created_at_utc");
                    table.CheckConstraint("ck_idempotency_records_operation_not_blank", "btrim(operation) <> ''");
                    table.CheckConstraint("ck_idempotency_records_request_hash_length", "length(request_hash) = 64");
                    table.ForeignKey(
                        name: "fk_idempotency_records_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_records_actor_user_id",
                schema: "public",
                table: "idempotency_records",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_records_expires_at_utc",
                schema: "public",
                table: "idempotency_records",
                column: "expires_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_records",
                schema: "public");
        }
    }
}
