using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Harden_M6_Custody_Invariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_custodies_personal_requires_employee",
                schema: "public",
                table: "custodies",
                sql: "custody_kind <> 'Personal' OR holder_type = 'Employee'");

            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_asset_movement_history_prevent_update
                    ON public.asset_movement_history;
                DROP FUNCTION IF EXISTS public.prevent_asset_movement_history_update();

                CREATE FUNCTION public.prevent_asset_movement_history_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    IF TG_OP = 'DELETE'
                       AND NOT EXISTS (
                           SELECT 1
                           FROM public.assets
                           WHERE id = OLD.asset_id)
                    THEN
                        RETURN OLD;
                    END IF;

                    RAISE EXCEPTION 'asset_movement_history rows are immutable';
                END;
                $function$;

                CREATE TRIGGER trg_asset_movement_history_prevent_mutation
                BEFORE UPDATE OR DELETE ON public.asset_movement_history
                FOR EACH ROW
                EXECUTE FUNCTION public.prevent_asset_movement_history_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_asset_movement_history_prevent_mutation
                    ON public.asset_movement_history;
                DROP FUNCTION IF EXISTS public.prevent_asset_movement_history_mutation();

                CREATE FUNCTION public.prevent_asset_movement_history_update()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION 'asset_movement_history rows are immutable';
                END;
                $function$;

                CREATE TRIGGER trg_asset_movement_history_prevent_update
                BEFORE UPDATE ON public.asset_movement_history
                FOR EACH ROW
                EXECUTE FUNCTION public.prevent_asset_movement_history_update();
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_custodies_personal_requires_employee",
                schema: "public",
                table: "custodies");
        }
    }
}
