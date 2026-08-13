using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Issue_And_Atomic_Transfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "issue_to",
                schema: "public",
                columns: table => new
                {
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_issue_to", x => x.document_id);
                    table.CheckConstraint("ck_issue_to_issue_reason_not_blank", "length(btrim(issue_reason)) > 0");
                    table.CheckConstraint("ck_issue_to_recipient_type_valid", "recipient_type IN ('Employee', 'OrganizationalUnit', 'Site', 'External')");
                    table.ForeignKey(
                        name: "fk_issue_to_warehouse_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "public",
                        principalTable: "warehouse_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transfer_info",
                schema: "public",
                columns: table => new
                {
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transfer_info", x => x.document_id);
                    table.CheckConstraint("ck_transfer_info_transfer_reason_not_blank", "length(btrim(transfer_reason)) > 0");
                    table.ForeignKey(
                        name: "fk_transfer_info_warehouse_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "public",
                        principalTable: "warehouse_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transfer_info_warehouses_destination_warehouse_id",
                        column: x => x.destination_warehouse_id,
                        principalSchema: "public",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_issue_to_recipient_type_recipient_id",
                schema: "public",
                table: "issue_to",
                columns: new[] { "recipient_type", "recipient_id" });

            migrationBuilder.CreateIndex(
                name: "ix_transfer_info_destination_warehouse_id",
                schema: "public",
                table: "transfer_info",
                column: "destination_warehouse_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "issue_to",
                schema: "public");

            migrationBuilder.DropTable(
                name: "transfer_info",
                schema: "public");
        }
    }
}
