using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionMappingAndPolicyVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "authorization_policy_versions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    active_vocabulary = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    mapping_version = table.Column<int>(type: "integer", nullable: false),
                    concurrency_token = table.Column<long>(type: "bigint", nullable: false),
                    activated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    activated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_authorization_policy_versions", x => x.id);
                    table.CheckConstraint("ck_authorization_policy_versions_concurrency_token_positive", "concurrency_token > 0");
                    table.CheckConstraint("ck_authorization_policy_versions_mapping_version_positive", "mapping_version > 0");
                    table.CheckConstraint("ck_authorization_policy_versions_strings_non_empty", "length(trim(active_vocabulary)) > 0 AND length(trim(activated_by)) > 0");
                    table.CheckConstraint("ck_authorization_policy_versions_vocabulary_supported", "active_vocabulary IN ('legacy-colon', 'dotted-v1')");
                });

            migrationBuilder.CreateTable(
                name: "permission_code_mappings",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    new_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    mapping_version = table.Column<int>(type: "integer", nullable: false),
                    approver = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    approved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permission_code_mappings", x => x.id);
                    table.CheckConstraint("ck_permission_code_mappings_codes_non_empty", "length(trim(old_code)) > 0 AND length(trim(new_code)) > 0 AND length(trim(rationale)) > 0");
                    table.CheckConstraint("ck_permission_code_mappings_mapping_version_positive", "mapping_version > 0");
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "authorization_policy_versions",
                columns: new[] { "id", "activated_at_utc", "activated_by", "active_vocabulary", "concurrency_token", "is_active", "mapping_version" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000901"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "phase-1-expand", "legacy-colon", 1L, true, 1 });

            migrationBuilder.InsertData(
                schema: "public",
                table: "permission_code_mappings",
                columns: new[] { "id", "approved_at_utc", "approver", "created_at_utc", "mapping_version", "new_code", "old_code", "rationale" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "asset.view", "assets:view", "Asset registry, movement, and custody-read intent." },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "audit.view", "audit-logs:view", "Audit-read intent; recipient policy limits the grant to AUDITOR." },
                    { new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "asset.view", "custody:view", "Asset read includes custody timeline and derived status." },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "custody.assign", "custody:manage", "Scoped custody assignment and transfer operation." },
                    { new Guid("00000000-0000-0000-0000-000000000005"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "organization.view", "organizations:view", "Consolidated organizational reference read." },
                    { new Guid("00000000-0000-0000-0000-000000000006"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "organization.view", "sites:view", "Consolidated organizational reference read." },
                    { new Guid("00000000-0000-0000-0000-000000000007"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "organization.view", "org-units:view", "Consolidated organizational reference read." },
                    { new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "organization.view", "employees:view", "Consolidated organizational reference read." },
                    { new Guid("00000000-0000-0000-0000-000000000009"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "organization.manage", "organizations:manage", "Consolidated structural organizational administration." },
                    { new Guid("00000000-0000-0000-0000-000000000010"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "organization.manage", "sites:manage", "Consolidated structural organizational administration." },
                    { new Guid("00000000-0000-0000-0000-000000000011"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "organization.manage", "org-units:manage", "Consolidated structural organizational administration." },
                    { new Guid("00000000-0000-0000-0000-000000000012"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "organization.manage", "employees:manage", "Consolidated structural organizational administration." },
                    { new Guid("00000000-0000-0000-0000-000000000013"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "admin.role.view", "roles:view", "Role and permission-catalog read." },
                    { new Guid("00000000-0000-0000-0000-000000000014"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "admin.role.manage", "roles:manage", "Role and role-permission administration." },
                    { new Guid("00000000-0000-0000-0000-000000000015"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "catalog.manage", "material-categories:manage", "Consolidated master-catalog management; target manage includes its read capability." },
                    { new Guid("00000000-0000-0000-0000-000000000016"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "catalog.manage", "material-domains:manage", "Consolidated master-catalog management; target manage includes its read capability." },
                    { new Guid("00000000-0000-0000-0000-000000000017"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "catalog.manage", "material-families:manage", "Consolidated master-catalog management; target manage includes its read capability." },
                    { new Guid("00000000-0000-0000-0000-000000000018"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "catalog.manage", "materials:manage", "Consolidated master-catalog management; target manage includes its read capability." },
                    { new Guid("00000000-0000-0000-0000-000000000019"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "catalog.manage", "units-of-measure:manage", "Consolidated master-catalog management; target manage includes its read capability." },
                    { new Guid("00000000-0000-0000-0000-000000000020"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "catalog.view", "materials:view", "Consolidated catalog read." },
                    { new Guid("00000000-0000-0000-0000-000000000021"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "catalog.view", "units-of-measure:view", "Consolidated catalog read." },
                    { new Guid("00000000-0000-0000-0000-000000000022"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "warehouse.manage", "warehouse-capabilities:manage", "Consolidated warehouse structure, capability, and settings management." },
                    { new Guid("00000000-0000-0000-0000-000000000023"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "warehouse.manage", "warehouse-material-settings:manage", "Consolidated warehouse structure, capability, and settings management." },
                    { new Guid("00000000-0000-0000-0000-000000000024"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "warehouse.manage", "warehouses:manage", "Consolidated warehouse structure, capability, and settings management." },
                    { new Guid("00000000-0000-0000-0000-000000000025"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "warehouse.view", "warehouses:view", "Warehouse, capability, and settings read." },
                    { new Guid("00000000-0000-0000-0000-000000000026"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "inventory.view", "inventory:view", "Balances and movement-ledger read intent." },
                    { new Guid("00000000-0000-0000-0000-000000000027"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "document.view", "warehouse-documents:view", "Shared document read including policy, history, and attachments." },
                    { new Guid("00000000-0000-0000-0000-000000000028"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "document.create", "warehouse-documents:create", "Start a document draft in its warehouse." },
                    { new Guid("00000000-0000-0000-0000-000000000029"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "document.update", "warehouse-documents:edit", "Change draft content and draft attachments." },
                    { new Guid("00000000-0000-0000-0000-000000000030"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "document.submit", "warehouse-documents:submit", "Draft to submitted lifecycle transition." },
                    { new Guid("00000000-0000-0000-0000-000000000031"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "document.cancel", "warehouse-documents:cancel", "Pre-post cancellation with actor and state restrictions." },
                    { new Guid("00000000-0000-0000-0000-000000000032"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "document.post", "warehouse-documents:review", "Submitted warehouse document posting review action." },
                    { new Guid("00000000-0000-0000-0000-000000000033"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "document.reject", "warehouse-documents:review", "Submitted warehouse document rejection review action; not revise." },
                    { new Guid("00000000-0000-0000-0000-000000000034"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "document.reverse", "warehouse-documents:reverse", "Governed compensating reversal." },
                    { new Guid("00000000-0000-0000-0000-000000000035"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "count.view", "inventory-counts:view", "Count, session, and variance read." },
                    { new Guid("00000000-0000-0000-0000-000000000036"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "count.plan", "inventory-counts:plan", "Count planning and creation." },
                    { new Guid("00000000-0000-0000-0000-000000000037"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "count.enter", "inventory-counts:enter-actual", "Keeper entry of count actuals." },
                    { new Guid("00000000-0000-0000-0000-000000000038"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "count.plan", "inventory-counts:review", "Broad review maps Start to count plan because v1 has no count.start." },
                    { new Guid("00000000-0000-0000-0000-000000000039"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "count.complete", "inventory-counts:review", "Manager count completion lifecycle action." },
                    { new Guid("00000000-0000-0000-0000-000000000040"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "count.close", "inventory-counts:review", "Manager count closure lifecycle action." },
                    { new Guid("00000000-0000-0000-0000-000000000041"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "admin.user.view", "users:access", "Existing user list and detail administration read." },
                    { new Guid("00000000-0000-0000-0000-000000000042"), new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), "human-owner-2026-09-13", new DateTime(2026, 9, 13, 0, 0, 0, 0, DateTimeKind.Utc), 1, "admin.user.manage", "users:access", "Existing user create and update administration." }
                });

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.reject_permission_code_mapping_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'permission_code_mappings is append-only'
                        USING ERRCODE = '55000';
                END;
                $$;

                CREATE TRIGGER trg_permission_code_mappings_immutable
                BEFORE UPDATE OR DELETE ON public.permission_code_mappings
                FOR EACH ROW EXECUTE FUNCTION public.reject_permission_code_mapping_mutation();
                """);

            migrationBuilder.CreateIndex(
                name: "ux_authorization_policy_versions_active",
                schema: "public",
                table: "authorization_policy_versions",
                column: "is_active",
                unique: true,
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_permission_code_mappings_mapping_version_new_code",
                schema: "public",
                table: "permission_code_mappings",
                columns: new[] { "mapping_version", "new_code" });

            migrationBuilder.CreateIndex(
                name: "ix_permission_code_mappings_mapping_version_old_code",
                schema: "public",
                table: "permission_code_mappings",
                columns: new[] { "mapping_version", "old_code" });

            migrationBuilder.CreateIndex(
                name: "ux_permission_code_mappings_old_new_version",
                schema: "public",
                table: "permission_code_mappings",
                columns: new[] { "old_code", "new_code", "mapping_version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_permission_code_mappings_immutable
                    ON public.permission_code_mappings;
                DROP FUNCTION IF EXISTS public.reject_permission_code_mapping_mutation();
                """);

            migrationBuilder.DropTable(
                name: "authorization_policy_versions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "permission_code_mappings",
                schema: "public");
        }
    }
}
