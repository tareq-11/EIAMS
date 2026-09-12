using Infrastructure.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912000003_MIG_CAT_01_Family_Classification_Dictionary")]
public sealed class MIG_CAT_01_Family_Classification_Dictionary : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // MIG-CAT-01 expand: nullable fields keep the legacy writer compatible.
        migrationBuilder.AddColumn<bool>(
            name: "family_optional",
            table: "materials",
            type: "boolean",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "family_exception_reason",
            table: "materials",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "family_exception_reason", table: "materials");
        migrationBuilder.DropColumn(name: "family_optional", table: "materials");
    }
}
