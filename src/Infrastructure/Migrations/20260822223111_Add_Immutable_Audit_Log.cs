using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Immutable_Audit_Log : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    command_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    summary = table.Column<string>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                    table.CheckConstraint("ck_audit_logs_action_not_blank", "btrim(action) <> ''");
                    table.CheckConstraint("ck_audit_logs_entity_type_not_blank", "btrim(entity_type) <> ''");
                    table.CheckConstraint("ck_audit_logs_summary_is_json_object", "summary IS NULL OR jsonb_typeof(summary) = 'object'");
                    table.CheckConstraint("ck_audit_logs_summary_max_bytes", "summary IS NULL OR octet_length(summary::text) <= 16384");
                    table.ForeignKey(
                        name: "fk_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_log_entries",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    audit_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    old_value = table.Column<string>(type: "text", nullable: true),
                    new_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log_entries", x => x.id);
                    table.CheckConstraint("ck_audit_log_entries_field_name_not_blank", "btrim(field_name) <> ''");
                    table.CheckConstraint("ck_audit_log_entries_values_distinct", "old_value IS DISTINCT FROM new_value");
                    table.ForeignKey(
                        name: "fk_audit_log_entries_audit_logs_audit_log_id",
                        column: x => x.audit_log_id,
                        principalSchema: "public",
                        principalTable: "audit_logs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "permissions",
                columns: new[] { "id", "code", "description" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000126"), "audit-logs:view", "View the immutable audit trail of system activity." });

            migrationBuilder.InsertData(
                schema: "public",
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000126"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entries_audit_log_id_field_name_id",
                schema: "public",
                table: "audit_log_entries",
                columns: new[] { "audit_log_id", "field_name", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entries_field_name_audit_log_id",
                schema: "public",
                table: "audit_log_entries",
                columns: new[] { "field_name", "audit_log_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_aggregate_type_aggregate_id_created_at_utc_id",
                schema: "public",
                table: "audit_logs",
                columns: new[] { "aggregate_type", "aggregate_id", "created_at_utc", "id" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_created_at_utc_id",
                schema: "public",
                table: "audit_logs",
                columns: new[] { "created_at_utc", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity_type_entity_id_created_at_utc_id",
                schema: "public",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id", "created_at_utc", "id" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_operation_id_created_at_utc_id",
                schema: "public",
                table: "audit_logs",
                columns: new[] { "operation_id", "created_at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_request_id",
                schema: "public",
                table: "audit_logs",
                column: "request_id",
                filter: "request_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_user_id_created_at_utc_id",
                schema: "public",
                table: "audit_logs",
                columns: new[] { "user_id", "created_at_utc", "id" },
                descending: new[] { false, true, true },
                filter: "user_id IS NOT NULL");

            // D-AUDIT-01: append-only ledger. A plain CHECK constraint cannot express "reject this
            // statement kind" - only a trigger can, so this is enforced here in addition to
            // AuditLog exposing no mutation domain method.
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION public.trg_audit_logs_reject_mutation()
                RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_logs is append-only: % is not allowed', TG_OP;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_audit_logs_append_only
                    BEFORE UPDATE OR DELETE ON public.audit_logs
                    FOR EACH ROW
                    EXECUTE FUNCTION public.trg_audit_logs_reject_mutation();
                """);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION public.trg_audit_log_entries_reject_mutation()
                RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_log_entries is append-only: % is not allowed', TG_OP;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_audit_log_entries_append_only
                    BEFORE UPDATE OR DELETE ON public.audit_log_entries
                    FOR EACH ROW
                    EXECUTE FUNCTION public.trg_audit_log_entries_reject_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_audit_log_entries_append_only ON public.audit_log_entries;");

            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS public.trg_audit_log_entries_reject_mutation();");

            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS trg_audit_logs_append_only ON public.audit_logs;");

            migrationBuilder.Sql(
                "DROP FUNCTION IF EXISTS public.trg_audit_logs_reject_mutation();");

            migrationBuilder.DropTable(
                name: "audit_log_entries",
                schema: "public");

            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "public");

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000126"), new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000126"));
        }
    }
}
