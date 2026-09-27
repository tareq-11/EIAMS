using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeCustodyHistoriesImmutable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE FUNCTION public.prevent_custody_history_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION '% rows are immutable', TG_TABLE_NAME
                        USING ERRCODE = '55000';
                END;
                $function$;

                CREATE TRIGGER trg_custody_history_prevent_mutation
                BEFORE UPDATE OR DELETE ON public.custody_history
                FOR EACH ROW
                EXECUTE FUNCTION public.prevent_custody_history_mutation();

                CREATE TRIGGER trg_durable_custody_histories_prevent_mutation
                BEFORE UPDATE OR DELETE ON public.durable_custody_histories
                FOR EACH ROW
                EXECUTE FUNCTION public.prevent_custody_history_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_durable_custody_histories_prevent_mutation
                    ON public.durable_custody_histories;
                DROP TRIGGER IF EXISTS trg_custody_history_prevent_mutation
                    ON public.custody_history;
                DROP FUNCTION IF EXISTS public.prevent_custody_history_mutation();
                """);
        }
    }
}
