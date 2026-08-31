using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialBaseUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "base_unit_id",
                schema: "public",
                table: "materials",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            // Backfill base_unit_id from material_families.base_unit_id
            migrationBuilder.Sql(@"
                UPDATE public.materials m
                SET base_unit_id = mf.base_unit_id
                FROM public.material_families mf
                WHERE m.family_id = mf.id;
            ");

            migrationBuilder.CreateIndex(
                name: "ix_materials_base_unit_id",
                schema: "public",
                table: "materials",
                column: "base_unit_id");

            migrationBuilder.AddForeignKey(
                name: "fk_materials_units_of_measure_base_unit_id",
                schema: "public",
                table: "materials",
                column: "base_unit_id",
                principalSchema: "public",
                principalTable: "units_of_measure",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_materials_units_of_measure_base_unit_id",
                schema: "public",
                table: "materials");

            migrationBuilder.DropIndex(
                name: "ix_materials_base_unit_id",
                schema: "public",
                table: "materials");

            migrationBuilder.DropColumn(
                name: "base_unit_id",
                schema: "public",
                table: "materials");
        }
    }
}
