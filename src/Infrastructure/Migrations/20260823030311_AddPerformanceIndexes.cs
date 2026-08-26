using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_warehouse_documents_warehouse_id",
                schema: "public",
                table: "warehouse_documents");

            migrationBuilder.DropIndex(
                name: "ix_stock_movements_warehouse_id_material_id",
                schema: "public",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "ix_employees_org_unit_id",
                schema: "public",
                table: "employees");

            migrationBuilder.CreateIndex(
                name: "ix_warehouse_documents_warehouse_id_document_status_created_at",
                schema: "public",
                table: "warehouse_documents",
                columns: new[] { "warehouse_id", "document_status", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_warehouse_id_material_id",
                schema: "public",
                table: "stock_movements",
                columns: new[] { "warehouse_id", "material_id" })
                .Annotation("Npgsql:IndexInclude", new[] { "quantity_delta" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_warehouse_id_posted_at_utc_id",
                schema: "public",
                table: "stock_movements",
                columns: new[] { "warehouse_id", "posted_at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_counts_warehouse_id_status_planned_at_utc",
                schema: "public",
                table: "inventory_counts",
                columns: new[] { "warehouse_id", "status", "planned_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_employees_org_unit_id_status_full_name",
                schema: "public",
                table: "employees",
                columns: new[] { "org_unit_id", "status", "full_name" });

            migrationBuilder.CreateIndex(
                name: "ix_document_attachments_document_id",
                schema: "public",
                table: "document_attachments",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_custodies_asset_id_from_utc_id",
                schema: "public",
                table: "custodies",
                columns: new[] { "asset_id", "from_utc", "id" },
                filter: "status = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_warehouse_documents_warehouse_id_document_status_created_at",
                schema: "public",
                table: "warehouse_documents");

            migrationBuilder.DropIndex(
                name: "ix_stock_movements_warehouse_id_material_id",
                schema: "public",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "ix_stock_movements_warehouse_id_posted_at_utc_id",
                schema: "public",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "ix_inventory_counts_warehouse_id_status_planned_at_utc",
                schema: "public",
                table: "inventory_counts");

            migrationBuilder.DropIndex(
                name: "ix_employees_org_unit_id_status_full_name",
                schema: "public",
                table: "employees");

            migrationBuilder.DropIndex(
                name: "ix_document_attachments_document_id",
                schema: "public",
                table: "document_attachments");

            migrationBuilder.DropIndex(
                name: "ix_custodies_asset_id_from_utc_id",
                schema: "public",
                table: "custodies");

            migrationBuilder.CreateIndex(
                name: "ix_warehouse_documents_warehouse_id",
                schema: "public",
                table: "warehouse_documents",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_warehouse_id_material_id",
                schema: "public",
                table: "stock_movements",
                columns: new[] { "warehouse_id", "material_id" });

            migrationBuilder.CreateIndex(
                name: "ix_employees_org_unit_id",
                schema: "public",
                table: "employees",
                column: "org_unit_id");
        }
    }
}
