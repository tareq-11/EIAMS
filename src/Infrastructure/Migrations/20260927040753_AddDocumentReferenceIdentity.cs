using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentReferenceIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "reference_sequence",
                schema: "public",
                table: "warehouse_documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reference_site_id",
                schema: "public",
                table: "warehouse_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "reference_year",
                schema: "public",
                table: "warehouse_documents",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_warehouse_documents_reference_site_id_reference_year_refere",
                schema: "public",
                table: "warehouse_documents",
                columns: new[] { "reference_site_id", "reference_year", "reference_sequence" },
                unique: true,
                filter: "reference_site_id IS NOT NULL AND reference_year IS NOT NULL AND reference_sequence IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_warehouse_documents_reference_identity_complete",
                schema: "public",
                table: "warehouse_documents",
                sql: "(reference_site_id IS NULL AND reference_year IS NULL AND reference_sequence IS NULL) OR (reference_site_id IS NOT NULL AND reference_year IS NOT NULL AND reference_sequence IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_warehouse_documents_reference_identity_values_valid",
                schema: "public",
                table: "warehouse_documents",
                sql: "reference_year IS NULL OR reference_year BETWEEN 2000 AND 9999");

            migrationBuilder.AddCheckConstraint(
                name: "ck_warehouse_documents_reference_sequence_positive",
                schema: "public",
                table: "warehouse_documents",
                sql: "reference_sequence IS NULL OR reference_sequence > 0");

            migrationBuilder.AddForeignKey(
                name: "fk_warehouse_documents_sites_reference_site_id",
                schema: "public",
                table: "warehouse_documents",
                column: "reference_site_id",
                principalSchema: "public",
                principalTable: "sites",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$ BEGIN
                    IF EXISTS (
                        SELECT 1 FROM public.warehouse_documents
                        WHERE reference_site_id IS NOT NULL
                           OR reference_year IS NOT NULL
                           OR reference_sequence IS NOT NULL
                    ) THEN
                        RAISE EXCEPTION
                            'document-reference-identity-rollback-blocked: allocated identities exist; restore from a reviewed backup or use a forward fix';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_warehouse_documents_sites_reference_site_id",
                schema: "public",
                table: "warehouse_documents");

            migrationBuilder.DropIndex(
                name: "ix_warehouse_documents_reference_site_id_reference_year_refere",
                schema: "public",
                table: "warehouse_documents");

            migrationBuilder.DropCheckConstraint(
                name: "ck_warehouse_documents_reference_identity_complete",
                schema: "public",
                table: "warehouse_documents");

            migrationBuilder.DropCheckConstraint(
                name: "ck_warehouse_documents_reference_identity_values_valid",
                schema: "public",
                table: "warehouse_documents");

            migrationBuilder.DropCheckConstraint(
                name: "ck_warehouse_documents_reference_sequence_positive",
                schema: "public",
                table: "warehouse_documents");

            migrationBuilder.DropColumn(
                name: "reference_sequence",
                schema: "public",
                table: "warehouse_documents");

            migrationBuilder.DropColumn(
                name: "reference_site_id",
                schema: "public",
                table: "warehouse_documents");

            migrationBuilder.DropColumn(
                name: "reference_year",
                schema: "public",
                table: "warehouse_documents");
        }
    }
}
