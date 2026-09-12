using Infrastructure.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912000004_MIG_UOM_01_Conversion_Provenance")]
public sealed class MIG_UOM_01_Conversion_Provenance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // MIG-UOM-01 expand: unit_id and base_quantity already store the selected
        // unit and calculated base quantity. Add only the missing immutable snapshots.
        migrationBuilder.AddColumn<Guid>(
            name: "conversion_id",
            table: "document_lines",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "conversion_factor",
            table: "document_lines",
            type: "numeric(18,6)",
            precision: 18,
            scale: 6,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "entered_quantity",
            table: "document_lines",
            type: "numeric(18,3)",
            precision: 18,
            scale: 3,
            nullable: true);

        migrationBuilder.Sql(@"
            CREATE INDEX ix_document_lines_conversion_id
            ON public.document_lines USING btree (conversion_id);
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            DROP INDEX IF EXISTS public.ix_document_lines_conversion_id;
        ");
        migrationBuilder.DropColumn(name: "entered_quantity", table: "document_lines");
        migrationBuilder.DropColumn(name: "conversion_factor", table: "document_lines");
        migrationBuilder.DropColumn(name: "conversion_id", table: "document_lines");
    }
}
