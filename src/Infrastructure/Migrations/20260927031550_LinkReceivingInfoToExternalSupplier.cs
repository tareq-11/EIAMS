using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LinkReceivingInfoToExternalSupplier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "supplier_party_id",
                schema: "public",
                table: "receiving_info",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_receiving_info_supplier_party_id",
                schema: "public",
                table: "receiving_info",
                column: "supplier_party_id");

            migrationBuilder.AddForeignKey(
                name: "fk_receiving_info_external_parties_supplier_party_id",
                schema: "public",
                table: "receiving_info",
                column: "supplier_party_id",
                principalSchema: "public",
                principalTable: "external_parties",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_receiving_info_external_parties_supplier_party_id",
                schema: "public",
                table: "receiving_info");

            migrationBuilder.DropIndex(
                name: "ix_receiving_info_supplier_party_id",
                schema: "public",
                table: "receiving_info");

            migrationBuilder.DropColumn(
                name: "supplier_party_id",
                schema: "public",
                table: "receiving_info");
        }
    }
}
