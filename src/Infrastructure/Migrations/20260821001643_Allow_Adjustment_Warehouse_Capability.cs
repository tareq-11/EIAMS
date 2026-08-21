using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Allow_Adjustment_Warehouse_Capability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_warehouse_capability_operations_operation_type_valid",
                schema: "public",
                table: "warehouse_capability_operations");

            migrationBuilder.AddCheckConstraint(
                name: "ck_warehouse_capability_operations_operation_type_valid",
                schema: "public",
                table: "warehouse_capability_operations",
                sql: "operation_type IN ('Receiving', 'Issue', 'Transfer', 'Count', 'Return', 'Adjustment')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_warehouse_capability_operations_operation_type_valid",
                schema: "public",
                table: "warehouse_capability_operations");

            migrationBuilder.AddCheckConstraint(
                name: "ck_warehouse_capability_operations_operation_type_valid",
                schema: "public",
                table: "warehouse_capability_operations",
                sql: "operation_type IN ('Receiving', 'Issue', 'Transfer', 'Count', 'Return')");
        }
    }
}
