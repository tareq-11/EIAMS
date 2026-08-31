using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImplementSignedOriginalArchival : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_document_attachments_signed_original",
                schema: "public",
                table: "document_attachments");

            migrationBuilder.AddColumn<DateTime>(
                name: "archived_at_utc",
                schema: "public",
                table: "document_attachments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "archived_by",
                schema: "public",
                table: "document_attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "public",
                table: "document_attachments",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "replaces_attachment_id",
                schema: "public",
                table: "document_attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_attachments_archived_by",
                schema: "public",
                table: "document_attachments",
                column: "archived_by");

            migrationBuilder.CreateIndex(
                name: "ix_document_attachments_replaces_attachment_id",
                schema: "public",
                table: "document_attachments",
                column: "replaces_attachment_id",
                unique: true,
                filter: "replaces_attachment_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_document_attachments_signed_original",
                schema: "public",
                table: "document_attachments",
                columns: new[] { "document_id", "attachment_type" },
                unique: true,
                filter: "attachment_type = 'SignedOriginal' AND is_active");

            migrationBuilder.AddCheckConstraint(
                name: "ck_document_attachments_archive_state",
                schema: "public",
                table: "document_attachments",
                sql: "(attachment_type = 'Supporting' AND is_active AND archived_at_utc IS NULL AND archived_by IS NULL AND replaces_attachment_id IS NULL) OR (attachment_type = 'SignedOriginal' AND ((is_active AND archived_at_utc IS NULL AND archived_by IS NULL) OR (NOT is_active AND archived_at_utc IS NOT NULL AND archived_by IS NOT NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_document_attachments_not_self_replacement",
                schema: "public",
                table: "document_attachments",
                sql: "replaces_attachment_id IS NULL OR replaces_attachment_id <> id");

            migrationBuilder.AddForeignKey(
                name: "fk_document_attachments_document_attachments_replaces_attachme",
                schema: "public",
                table: "document_attachments",
                column: "replaces_attachment_id",
                principalSchema: "public",
                principalTable: "document_attachments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_document_attachments_users_archived_by",
                schema: "public",
                table: "document_attachments",
                column: "archived_by",
                principalSchema: "public",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Keep the database posting gate aligned with the Application policy: an archived
            // SignedOriginal remains historical evidence but cannot authorize posting.
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION public.trg_warehouse_documents_validate_signed_copy()
                RETURNS trigger AS $$
                BEGIN
                    IF NEW.document_status IN ('Posted', 'Reversed') THEN
                        IF NOT EXISTS (
                            SELECT 1 FROM public.document_attachments a
                            WHERE a.id = NEW.signed_copy_attachment_id
                              AND a.attachment_type = 'SignedOriginal'
                              AND a.document_id = NEW.id
                              AND a.is_active
                        ) THEN
                            RAISE EXCEPTION
                                'warehouse_documents % cannot become % without an active SignedOriginal attachment',
                                NEW.id, NEW.document_status;
                        END IF;
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The previous schema cannot represent replacement history. Refuse a lossy rollback
            // once a document has more than one preserved SignedOriginal.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM public.document_attachments
                        WHERE attachment_type = 'SignedOriginal'
                        GROUP BY document_id
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot roll back SignedOriginal archival while replacement history exists';
                    END IF;
                END;
                $$;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION public.trg_warehouse_documents_validate_signed_copy()
                RETURNS trigger AS $$
                BEGIN
                    IF NEW.document_status IN ('Posted', 'Reversed') THEN
                        IF NOT EXISTS (
                            SELECT 1 FROM public.document_attachments a
                            WHERE a.id = NEW.signed_copy_attachment_id
                              AND a.attachment_type = 'SignedOriginal'
                              AND a.document_id = NEW.id
                        ) THEN
                            RAISE EXCEPTION
                                'warehouse_documents % cannot become % without a SignedOriginal attachment',
                                NEW.id, NEW.document_status;
                        END IF;
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_document_attachments_document_attachments_replaces_attachme",
                schema: "public",
                table: "document_attachments");

            migrationBuilder.DropForeignKey(
                name: "fk_document_attachments_users_archived_by",
                schema: "public",
                table: "document_attachments");

            migrationBuilder.DropIndex(
                name: "ix_document_attachments_archived_by",
                schema: "public",
                table: "document_attachments");

            migrationBuilder.DropIndex(
                name: "ix_document_attachments_replaces_attachment_id",
                schema: "public",
                table: "document_attachments");

            migrationBuilder.DropIndex(
                name: "ux_document_attachments_signed_original",
                schema: "public",
                table: "document_attachments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_document_attachments_archive_state",
                schema: "public",
                table: "document_attachments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_document_attachments_not_self_replacement",
                schema: "public",
                table: "document_attachments");

            migrationBuilder.DropColumn(
                name: "archived_at_utc",
                schema: "public",
                table: "document_attachments");

            migrationBuilder.DropColumn(
                name: "archived_by",
                schema: "public",
                table: "document_attachments");

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "public",
                table: "document_attachments");

            migrationBuilder.DropColumn(
                name: "replaces_attachment_id",
                schema: "public",
                table: "document_attachments");

            migrationBuilder.CreateIndex(
                name: "ux_document_attachments_signed_original",
                schema: "public",
                table: "document_attachments",
                columns: new[] { "document_id", "attachment_type" },
                unique: true,
                filter: "attachment_type = 'SignedOriginal'");
        }
    }
}
