using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImplementSingleHierarchicalScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    conflicting_users text;
                BEGIN
                    SELECT string_agg(user_id::text || ' (' || assignment_count || ')', ', ' ORDER BY user_id)
                    INTO conflicting_users
                    FROM (
                        SELECT user_id, count(*) AS assignment_count
                        FROM public.user_role_scopes
                        GROUP BY user_id
                        HAVING count(*) > 1
                    ) AS conflicts;

                    IF conflicting_users IS NOT NULL THEN
                        RAISE EXCEPTION 'Phase 1 cannot enforce one assignment per user'
                            USING DETAIL = conflicting_users,
                                  HINT = 'Run scripts/phase1-authorization-preflight.sql and explicitly resolve every conflicting user.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_user_role_scopes_roles_role_id",
                schema: "public",
                table: "user_role_scopes");

            migrationBuilder.DropForeignKey(
                name: "fk_user_role_scopes_users_user_id",
                schema: "public",
                table: "user_role_scopes");

            migrationBuilder.DropIndex(
                name: "ux_user_role_scopes_enterprise",
                schema: "public",
                table: "user_role_scopes");

            migrationBuilder.DropIndex(
                name: "ux_user_role_scopes_scoped",
                schema: "public",
                table: "user_role_scopes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_role_scopes_scope_id",
                schema: "public",
                table: "user_role_scopes");

            migrationBuilder.AddColumn<Guid>(
                name: "organizational_unit_id",
                schema: "public",
                table: "warehouses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "scope_type",
                schema: "public",
                table: "user_role_scopes",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateTable(
                name: "role_allowed_scope_types",
                schema: "public",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_allowed_scope_types", x => new { x.role_id, x.scope_type });
                    table.ForeignKey(
                        name: "fk_role_allowed_scope_types_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "public",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "role_allowed_scope_types",
                columns: new[] { "role_id", "scope_type" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), "Enterprise" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "Warehouse" },
                    { new Guid("00000000-0000-0000-0000-000000000003"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000003"), "Warehouse" }
                });

            migrationBuilder.Sql(
                """
                INSERT INTO public.role_allowed_scope_types (role_id, scope_type)
                SELECT DISTINCT assignment.role_id, assignment.scope_type
                FROM public.user_role_scopes AS assignment
                WHERE assignment.role_id NOT IN (
                    '00000000-0000-0000-0000-000000000001'::uuid,
                    '00000000-0000-0000-0000-000000000002'::uuid,
                    '00000000-0000-0000-0000-000000000003'::uuid)
                ON CONFLICT DO NOTHING;

                INSERT INTO public.role_allowed_scope_types (role_id, scope_type)
                SELECT role.id, 'Enterprise'
                FROM public.roles AS role
                WHERE role.id NOT IN (
                    '00000000-0000-0000-0000-000000000001'::uuid,
                    '00000000-0000-0000-0000-000000000002'::uuid,
                    '00000000-0000-0000-0000-000000000003'::uuid)
                  AND NOT EXISTS (
                    SELECT 1
                    FROM public.role_allowed_scope_types AS allowed
                    WHERE allowed.role_id = role.id)
                ON CONFLICT DO NOTHING;

                DO $$
                DECLARE
                    incompatible_assignments text;
                BEGIN
                    SELECT string_agg(
                        assignment.user_id::text || ':' || assignment.role_id::text || ':' || assignment.scope_type,
                        ', ' ORDER BY assignment.user_id)
                    INTO incompatible_assignments
                    FROM public.user_role_scopes AS assignment
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM public.role_allowed_scope_types AS allowed
                        WHERE allowed.role_id = assignment.role_id
                          AND allowed.scope_type = assignment.scope_type);

                    IF incompatible_assignments IS NOT NULL THEN
                        RAISE EXCEPTION 'Existing assignments violate the approved Role-to-Scope policy'
                            USING DETAIL = incompatible_assignments,
                                  HINT = 'Resolve the listed assignments explicitly before applying Phase 1.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.Sql(
                """
                WITH single_active_owner AS (
                    SELECT site_id, (array_agg(id ORDER BY id))[1] AS organizational_unit_id
                    FROM public.organizational_units
                    WHERE status = 'Active'
                    GROUP BY site_id
                    HAVING count(*) = 1
                )
                UPDATE public.warehouses AS warehouse
                SET organizational_unit_id = owner.organizational_unit_id
                FROM single_active_owner AS owner
                WHERE warehouse.site_id = owner.site_id
                  AND warehouse.organizational_unit_id IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_warehouses_organizational_unit_id",
                schema: "public",
                table: "warehouses",
                column: "organizational_unit_id");

            migrationBuilder.CreateIndex(
                name: "ux_user_role_scopes_user_id",
                schema: "public",
                table: "user_role_scopes",
                column: "user_id",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_role_scopes_scope_id",
                schema: "public",
                table: "user_role_scopes",
                sql: "(scope_type = 'Enterprise' AND scope_id IS NULL) OR (scope_type IN ('Site', 'OrganizationalUnit', 'Warehouse') AND scope_id IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_user_role_scopes_roles_role_id",
                schema: "public",
                table: "user_role_scopes",
                column: "role_id",
                principalSchema: "public",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_user_role_scopes_users_user_id",
                schema: "public",
                table: "user_role_scopes",
                column: "user_id",
                principalSchema: "public",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_warehouses_organizational_units_organizational_unit_id",
                schema: "public",
                table: "warehouses",
                column: "organizational_unit_id",
                principalSchema: "public",
                principalTable: "organizational_units",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE VIEW public.v_phase1_scope_migration_report AS
                SELECT
                    'USER_WITHOUT_ASSIGNMENT'::text AS issue_type,
                    users.id AS entity_id,
                    jsonb_build_object('email', users.email) AS details
                FROM public.users
                WHERE NOT EXISTS (
                    SELECT 1 FROM public.user_role_scopes AS assignment
                    WHERE assignment.user_id = users.id)

                UNION ALL

                SELECT
                    'WAREHOUSE_WITHOUT_ORGANIZATIONAL_UNIT'::text,
                    warehouse.id,
                    jsonb_build_object('site_id', warehouse.site_id, 'code', warehouse.code)
                FROM public.warehouses AS warehouse
                WHERE warehouse.organizational_unit_id IS NULL

                UNION ALL

                SELECT
                    'WAREHOUSE_ORGANIZATIONAL_UNIT_CROSS_SITE'::text,
                    warehouse.id,
                    jsonb_build_object(
                        'warehouse_site_id', warehouse.site_id,
                        'organizational_unit_id', warehouse.organizational_unit_id,
                        'organizational_unit_site_id', unit.site_id)
                FROM public.warehouses AS warehouse
                INNER JOIN public.organizational_units AS unit
                    ON unit.id = warehouse.organizational_unit_id
                WHERE warehouse.site_id <> unit.site_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS public.v_phase1_scope_migration_report;");

            migrationBuilder.DropForeignKey(
                name: "fk_user_role_scopes_roles_role_id",
                schema: "public",
                table: "user_role_scopes");

            migrationBuilder.DropForeignKey(
                name: "fk_user_role_scopes_users_user_id",
                schema: "public",
                table: "user_role_scopes");

            migrationBuilder.DropForeignKey(
                name: "fk_warehouses_organizational_units_organizational_unit_id",
                schema: "public",
                table: "warehouses");

            migrationBuilder.DropTable(
                name: "role_allowed_scope_types",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "ix_warehouses_organizational_unit_id",
                schema: "public",
                table: "warehouses");

            migrationBuilder.DropIndex(
                name: "ux_user_role_scopes_user_id",
                schema: "public",
                table: "user_role_scopes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_role_scopes_scope_id",
                schema: "public",
                table: "user_role_scopes");

            migrationBuilder.DropColumn(
                name: "organizational_unit_id",
                schema: "public",
                table: "warehouses");

            migrationBuilder.AlterColumn<string>(
                name: "scope_type",
                schema: "public",
                table: "user_role_scopes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.CreateIndex(
                name: "ux_user_role_scopes_enterprise",
                schema: "public",
                table: "user_role_scopes",
                columns: new[] { "user_id", "role_id", "scope_type" },
                unique: true,
                filter: "scope_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_user_role_scopes_scoped",
                schema: "public",
                table: "user_role_scopes",
                columns: new[] { "user_id", "role_id", "scope_type", "scope_id" },
                unique: true,
                filter: "scope_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_role_scopes_scope_id",
                schema: "public",
                table: "user_role_scopes",
                sql: "(scope_type = 'Enterprise' AND scope_id IS NULL) OR (scope_type IN ('Site', 'Warehouse') AND scope_id IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_user_role_scopes_roles_role_id",
                schema: "public",
                table: "user_role_scopes",
                column: "role_id",
                principalSchema: "public",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_user_role_scopes_users_user_id",
                schema: "public",
                table: "user_role_scopes",
                column: "user_id",
                principalSchema: "public",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
