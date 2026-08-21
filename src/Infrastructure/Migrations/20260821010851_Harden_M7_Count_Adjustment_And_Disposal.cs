using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Harden_M7_Count_Adjustment_And_Disposal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_inventory_count_lines_inventory_counts_count_id",
                schema: "public",
                table: "inventory_count_lines");

            migrationBuilder.AddCheckConstraint(
                name: "ck_inventory_adjustments_disposal_terminal",
                schema: "public",
                table: "inventory_adjustments",
                sql: "NOT (adjustment_kind = 'Disposal' AND status = 'Reversed')");

            migrationBuilder.AddForeignKey(
                name: "fk_inventory_count_lines_inventory_counts_count_id",
                schema: "public",
                table: "inventory_count_lines",
                column: "count_id",
                principalSchema: "public",
                principalTable: "inventory_counts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_inventory_count_lines_inventory_counts_count_id",
                schema: "public",
                table: "inventory_count_lines");

            migrationBuilder.DropCheckConstraint(
                name: "ck_inventory_adjustments_disposal_terminal",
                schema: "public",
                table: "inventory_adjustments");

            migrationBuilder.AddForeignKey(
                name: "fk_inventory_count_lines_inventory_counts_count_id",
                schema: "public",
                table: "inventory_count_lines",
                column: "count_id",
                principalSchema: "public",
                principalTable: "inventory_counts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
