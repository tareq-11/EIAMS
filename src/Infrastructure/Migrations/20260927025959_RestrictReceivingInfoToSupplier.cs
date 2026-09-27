using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestrictReceivingInfoToSupplier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "receiving_info_legacy_classifications",
                schema: "public",
                columns: table => new
                {
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receiving_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    supplier_ref = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supplier_invoice_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    classification = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    archived_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => table.PrimaryKey("pk_receiving_info_legacy_classifications", x => x.document_id));

            migrationBuilder.CreateIndex(
                name: "ix_receiving_info_legacy_classifications_receiving_type",
                schema: "public",
                table: "receiving_info_legacy_classifications",
                column: "receiving_type");

            // Stop writes while we classify and cut over. Free-form refs are never interpreted as
            // warehouse/document IDs. Only rows already represented by a matching typed detail can
            // be archived automatically; anything else aborts the migration without altering rows.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE ambiguous_count bigint;
                BEGIN
                    LOCK TABLE public.receiving_info, public.warehouse_documents,
                               public.transfer_info, public.return_info
                        IN SHARE ROW EXCLUSIVE MODE;

                    SELECT count(*) INTO ambiguous_count
                    FROM public.receiving_info AS ri
                    LEFT JOIN public.warehouse_documents AS wd ON wd.id = ri.document_id
                    LEFT JOIN public.transfer_info AS ti ON ti.document_id = ri.document_id
                    LEFT JOIN public.return_info AS ret ON ret.document_id = ri.document_id
                    WHERE NOT (
                        (ri.receiving_type = 'Supplier'
                         AND wd.document_type = 'Receiving'
                         AND ti.document_id IS NULL
                         AND ret.document_id IS NULL)
                        OR
                        (ri.receiving_type = 'Transfer'
                         AND wd.document_type = 'Transfer'
                         AND ti.document_id IS NOT NULL
                         AND ret.document_id IS NULL)
                        OR
                        (ri.receiving_type = 'Return'
                         AND wd.document_type = 'Return'
                         AND ret.document_id IS NOT NULL
                         AND ti.document_id IS NULL)
                    );

                    IF ambiguous_count > 0 THEN
                        RAISE EXCEPTION
                            'receiving-type-preflight-failed: % ambiguous rows; run scripts/phase4b-receiving-type-preflight.sql and remediate explicitly',
                            ambiguous_count
                            USING ERRCODE = '23514', CONSTRAINT = 'ck_receiving_info_supplier_only_preflight';
                    END IF;

                    INSERT INTO public.receiving_info_legacy_classifications (
                        document_id, receiving_type, supplier_ref, supplier_invoice_ref,
                        created_at_utc, created_by, updated_at_utc, updated_by,
                        classification, archived_at_utc)
                    SELECT
                        ri.document_id, ri.receiving_type, ri.supplier_ref, ri.supplier_invoice_ref,
                        ri.created_at_utc, ri.created_by, ri.updated_at_utc, ri.updated_by,
                        CASE ri.receiving_type
                            WHEN 'Transfer' THEN 'AlreadyRepresentedByTransferInfo'
                            WHEN 'Return' THEN 'AlreadyRepresentedByReturnInfo'
                        END,
                        now()
                    FROM public.receiving_info AS ri
                    WHERE ri.receiving_type IN ('Transfer', 'Return');

                    DELETE FROM public.receiving_info AS ri
                    WHERE ri.receiving_type IN ('Transfer', 'Return');

                    IF EXISTS (SELECT 1 FROM public.receiving_info WHERE receiving_type <> 'Supplier') THEN
                        RAISE EXCEPTION 'receiving-type-cutover-failed: non-Supplier rows remain'
                            USING ERRCODE = '23514', CONSTRAINT = 'ck_receiving_info_supplier_only_preflight';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_receiving_info_receiving_type_valid",
                schema: "public",
                table: "receiving_info");

            migrationBuilder.AddCheckConstraint(
                name: "ck_receiving_info_receiving_type_valid",
                schema: "public",
                table: "receiving_info",
                sql: "receiving_type = 'Supplier'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM public.receiving_info_legacy_classifications) THEN
                        RAISE EXCEPTION
                            'receiving-type-rollback-blocked: legacy Transfer/Return rows remain archived; restore a reviewed backup or use a forward-fix'
                            USING ERRCODE = '23514', CONSTRAINT = 'ck_receiving_info_legacy_archive_preserved';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_receiving_info_receiving_type_valid",
                schema: "public",
                table: "receiving_info");

            migrationBuilder.AddCheckConstraint(
                name: "ck_receiving_info_receiving_type_valid",
                schema: "public",
                table: "receiving_info",
                sql: "receiving_type IN ('Supplier', 'Transfer', 'Return')");

            migrationBuilder.DropTable(
                name: "receiving_info_legacy_classifications",
                schema: "public");
        }
    }
}
