using Infrastructure.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912000011_MIG_RBAC_01_Dotted_Permissions")]
public sealed class MIG_RBAC_01_Dotted_Permissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "permission_code_mappings",
            schema: "public",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                old_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                new_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                mapping_version = table.Column<int>(type: "integer", nullable: false),
                mapped_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                mapped_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                notes = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_permission_code_mappings", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ux_permission_code_mappings_old_code_version",
            schema: "public",
            table: "permission_code_mappings",
            columns: new[] { "old_code", "mapping_version" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_permission_code_mappings_new_code_version",
            schema: "public",
            table: "permission_code_mappings",
            columns: new[] { "new_code", "mapping_version" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "permission_code_mappings", schema: "public");
}
