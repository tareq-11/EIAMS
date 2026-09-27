using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UnifyDocumentSequencesBySiteYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve the highest allocated number for every site/year. Counter rows are
            // implementation state, not document history; issued references remain unchanged.
            // Taking the max ensures the unified allocator starts above every old per-type counter.
            migrationBuilder.Sql(
                """
                LOCK TABLE public.document_sequences IN SHARE ROW EXCLUSIVE MODE;

                WITH ranked AS (
                    SELECT id,
                           row_number() OVER (
                               PARTITION BY site_id, year
                               ORDER BY last_sequence DESC, id ASC) AS position
                    FROM public.document_sequences
                )
                DELETE FROM public.document_sequences AS sequence
                USING ranked
                WHERE sequence.id = ranked.id AND ranked.position > 1;
                """);

            migrationBuilder.DropIndex(
                name: "ix_document_sequences_site_id_document_type_year",
                schema: "public",
                table: "document_sequences");

            migrationBuilder.DropCheckConstraint(
                name: "ck_document_sequences_document_type_valid",
                schema: "public",
                table: "document_sequences");

            migrationBuilder.DropColumn(
                name: "document_type",
                schema: "public",
                table: "document_sequences");

            migrationBuilder.CreateIndex(
                name: "ix_document_sequences_site_id_year",
                schema: "public",
                table: "document_sequences",
                columns: new[] { "site_id", "year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$ BEGIN
                    RAISE EXCEPTION
                        'document-sequence-rollback-blocked: per-type counters were consolidated; restoring them requires a reviewed backup or forward fix';
                END $$;
                """);
        }
    }
}
