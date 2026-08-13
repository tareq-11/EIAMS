using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixM4ReviewFindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_assets_document_lines_receipt_line_id",
                schema: "public",
                table: "assets");

            migrationBuilder.AddUniqueConstraint(
                name: "ak_document_lines_id_material_id",
                schema: "public",
                table: "document_lines",
                columns: new[] { "id", "material_id" });

            migrationBuilder.CreateIndex(
                name: "ix_assets_receipt_line_id_material_id",
                schema: "public",
                table: "assets",
                columns: new[] { "receipt_line_id", "material_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_assets_document_lines_receipt_line_id_material_id",
                schema: "public",
                table: "assets",
                columns: new[] { "receipt_line_id", "material_id" },
                principalSchema: "public",
                principalTable: "document_lines",
                principalColumns: new[] { "id", "material_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_assets_document_lines_receipt_line_id_material_id",
                schema: "public",
                table: "assets");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_document_lines_id_material_id",
                schema: "public",
                table: "document_lines");

            migrationBuilder.DropIndex(
                name: "ix_assets_receipt_line_id_material_id",
                schema: "public",
                table: "assets");

            migrationBuilder.AddForeignKey(
                name: "fk_assets_document_lines_receipt_line_id",
                schema: "public",
                table: "assets",
                column: "receipt_line_id",
                principalSchema: "public",
                principalTable: "document_lines",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
