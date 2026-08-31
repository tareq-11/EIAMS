using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImplementDocumentLifecycleHistoryAndPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_lifecycle_events",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    to_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resulting_row_version = table.Column<int>(type: "integer", nullable: false),
                    request_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_lifecycle_events", x => x.id);
                    table.CheckConstraint("ck_document_lifecycle_events_action_not_blank", "btrim(action) <> ''");
                    table.CheckConstraint("ck_document_lifecycle_events_actor_not_blank", "btrim(actor_display_name) <> ''");
                    table.CheckConstraint("ck_document_lifecycle_events_row_version_positive", "resulting_row_version > 0");
                    table.ForeignKey(
                        name: "fk_document_lifecycle_events_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_document_lifecycle_events_warehouse_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "public",
                        principalTable: "warehouse_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_lifecycle_events_actor_user_id",
                schema: "public",
                table: "document_lifecycle_events",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_lifecycle_events_document_id_action_operation_id",
                schema: "public",
                table: "document_lifecycle_events",
                columns: new[] { "document_id", "action", "operation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_lifecycle_events_document_id_occurred_at_utc_id",
                schema: "public",
                table: "document_lifecycle_events",
                columns: new[] { "document_id", "occurred_at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_document_lifecycle_events_document_id_resulting_row_version",
                schema: "public",
                table: "document_lifecycle_events",
                columns: new[] { "document_id", "resulting_row_version", "action" },
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION public.trg_document_lifecycle_events_reject_mutation()
                RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'document_lifecycle_events is append-only: % is not allowed', TG_OP;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_document_lifecycle_events_append_only
                    BEFORE UPDATE OR DELETE ON public.document_lifecycle_events
                    FOR EACH ROW
                    EXECUTE FUNCTION public.trg_document_lifecycle_events_reject_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_document_lifecycle_events_append_only ON public.document_lifecycle_events;");

            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS public.trg_document_lifecycle_events_reject_mutation();");

            migrationBuilder.DropTable(
                name: "document_lifecycle_events",
                schema: "public");
        }
    }
}
