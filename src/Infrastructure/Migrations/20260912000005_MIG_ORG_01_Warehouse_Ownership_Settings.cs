using Infrastructure.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912000005_MIG_ORG_01_Warehouse_Ownership_Settings")]
public sealed class MIG_ORG_01_Warehouse_Ownership_Settings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The warehouse/material unique index already exists. Expand only the
        // missing optimistic-concurrency field used by the target writer.
        migrationBuilder.AddColumn<int>(
            name: "row_version",
            table: "warehouse_material_settings",
            type: "integer",
            nullable: false,
            defaultValue: 1);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "row_version", table: "warehouse_material_settings");
    }
}
