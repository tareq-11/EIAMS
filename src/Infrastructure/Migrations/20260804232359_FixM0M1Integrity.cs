using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixM0M1Integrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_employee_id",
                schema: "public",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_user_role_scopes_user_id_role_id_scope_type_scope_id",
                schema: "public",
                table: "user_role_scopes");

            migrationBuilder.DropIndex(
                name: "ix_material_unit_conversions_material_id",
                schema: "public",
                table: "material_unit_conversions");

            migrationBuilder.CreateIndex(
                name: "ix_users_employee_id",
                schema: "public",
                table: "users",
                column: "employee_id",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "ix_material_unit_conversions_material_id_from_unit_id",
                schema: "public",
                table: "material_unit_conversions",
                columns: new[] { "material_id", "from_unit_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_material_unit_conversions_positive_factor",
                schema: "public",
                table: "material_unit_conversions",
                sql: "factor > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_employee_id",
                schema: "public",
                table: "users");

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

            migrationBuilder.DropIndex(
                name: "ix_material_unit_conversions_material_id_from_unit_id",
                schema: "public",
                table: "material_unit_conversions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_material_unit_conversions_positive_factor",
                schema: "public",
                table: "material_unit_conversions");

            migrationBuilder.CreateIndex(
                name: "ix_users_employee_id",
                schema: "public",
                table: "users",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_role_scopes_user_id_role_id_scope_type_scope_id",
                schema: "public",
                table: "user_role_scopes",
                columns: new[] { "user_id", "role_id", "scope_type", "scope_id" });

            migrationBuilder.CreateIndex(
                name: "ix_material_unit_conversions_material_id",
                schema: "public",
                table: "material_unit_conversions",
                column: "material_id");
        }
    }
}
