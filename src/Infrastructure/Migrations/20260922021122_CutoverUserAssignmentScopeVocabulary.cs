using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CutoverUserAssignmentScopeVocabulary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM public.user_role_scopes
                        WHERE scope_type = 'OrganizationalUnit') THEN
                        RAISE EXCEPTION '1D cutover blocked: user_role_scopes contains OrganizationalUnit assignments; remediate manually before retrying';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM public.users u
                        LEFT JOIN public.user_role_scopes s ON s.user_id = u.id
                        GROUP BY u.id, u.status
                        HAVING count(s.id) > 1 OR (u.status = 'Active' AND count(s.id) = 0)) THEN
                        RAISE EXCEPTION '1D cutover blocked: active users need one assignment and no user may have multiple assignments';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM public.user_role_scopes s
                        WHERE (s.scope_type = 'Enterprise' AND s.scope_id IS NOT NULL)
                           OR (s.scope_type IN ('Site', 'Warehouse') AND s.scope_id IS NULL)
                           OR (s.scope_type = 'Site' AND NOT EXISTS (SELECT 1 FROM public.sites x WHERE x.id = s.scope_id))
                           OR (s.scope_type = 'Warehouse' AND NOT EXISTS (SELECT 1 FROM public.warehouses x WHERE x.id = s.scope_id))
                           OR s.scope_type NOT IN ('Enterprise', 'Site', 'Warehouse')) THEN
                        RAISE EXCEPTION '1D cutover blocked: assignment scope id shape or target is invalid';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM public.user_role_scopes s
                        LEFT JOIN public.role_allowed_scope_types a
                          ON a.role_id = s.role_id AND a.scope_type = s.scope_type
                        WHERE a.role_id IS NULL) THEN
                        RAISE EXCEPTION '1D cutover blocked: assignment role/scope compatibility is invalid';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("""
                DELETE FROM public.permission_allowed_scope_types
                WHERE scope_type = 'OrganizationalUnit';
                DELETE FROM public.role_allowed_scope_types
                WHERE scope_type = 'OrganizationalUnit';
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_role_scopes_scope_id",
                schema: "public",
                table: "user_role_scopes");

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000115"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000122"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000126"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000127"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000128"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000129"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000130"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000132"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000133"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000134"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000135"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000136"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000137"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000201"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000202"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000204"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000209"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000211"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000212"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000213"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000221"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "permission_allowed_scope_types",
                keyColumns: new[] { "permission_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000229"), "OrganizationalUnit" });

            migrationBuilder.DeleteData(
                schema: "public",
                table: "role_allowed_scope_types",
                keyColumns: new[] { "role_id", "scope_type" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), "OrganizationalUnit" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_role_scopes_scope_id",
                schema: "public",
                table: "user_role_scopes",
                sql: "(scope_type = 'Enterprise' AND scope_id IS NULL) OR (scope_type IN ('Site', 'Warehouse') AND scope_id IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_user_role_scopes_scope_id",
                schema: "public",
                table: "user_role_scopes");

            migrationBuilder.InsertData(
                schema: "public",
                table: "permission_allowed_scope_types",
                columns: new[] { "permission_id", "scope_type" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000115"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000122"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000126"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000127"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000128"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000129"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000130"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000132"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000133"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000134"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000135"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000136"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000137"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000201"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000202"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000204"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000209"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000211"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000212"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000213"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000221"), "OrganizationalUnit" },
                    { new Guid("00000000-0000-0000-0000-000000000229"), "OrganizationalUnit" }
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "role_allowed_scope_types",
                columns: new[] { "role_id", "scope_type" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), "OrganizationalUnit" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_role_scopes_scope_id",
                schema: "public",
                table: "user_role_scopes",
                sql: "(scope_type = 'Enterprise' AND scope_id IS NULL) OR (scope_type IN ('Site', 'OrganizationalUnit', 'Warehouse') AND scope_id IS NOT NULL)");
        }
    }
}
