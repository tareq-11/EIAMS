using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeInventoryCountMembershipImmutable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$ BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM public.inventory_counts AS c
                        WHERE c.status IN ('InProgress', 'Completed', 'Closed')
                          AND NOT EXISTS (
                              SELECT 1 FROM public.inventory_count_lines AS line WHERE line.count_id = c.id)
                    ) THEN
                        RAISE EXCEPTION
                            'inventory-count-membership-preflight-failed: an active or terminal count has no immutable line snapshot';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_inventory_counts_status_valid",
                schema: "public",
                table: "inventory_counts");

            migrationBuilder.AddColumn<DateTime>(
                name: "aborted_at_utc",
                schema: "public",
                table: "inventory_counts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_inventory_counts_aborted_timestamp",
                schema: "public",
                table: "inventory_counts",
                sql: "(status = 'Aborted' AND aborted_at_utc IS NOT NULL AND aborted_at_utc >= COALESCE(started_at_utc, planned_at_utc)) OR (status <> 'Aborted' AND aborted_at_utc IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_inventory_counts_status_valid",
                schema: "public",
                table: "inventory_counts",
                sql: "status IN ('Planned', 'InProgress', 'Completed', 'Closed', 'Aborted')");

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION public.guard_inventory_count_line_membership()
                RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE parent_status text;
                BEGIN
                    SELECT status INTO parent_status
                    FROM public.inventory_counts
                    WHERE id = CASE WHEN TG_OP = 'DELETE' THEN OLD.count_id ELSE NEW.count_id END
                    FOR UPDATE;

                    IF parent_status IS NULL THEN
                        RAISE EXCEPTION 'inventory-count-membership-parent-missing';
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        IF parent_status <> 'Planned' THEN
                            RAISE EXCEPTION 'inventory-count-membership-locked';
                        END IF;
                        RETURN NEW;
                    ELSIF TG_OP = 'DELETE' THEN
                        IF parent_status <> 'Planned' THEN
                            RAISE EXCEPTION 'inventory-count-membership-locked';
                        END IF;
                        RETURN OLD;
                    ELSE
                        IF OLD.count_id IS DISTINCT FROM NEW.count_id
                           OR OLD.material_id IS DISTINCT FROM NEW.material_id
                           OR OLD.asset_id IS DISTINCT FROM NEW.asset_id
                           OR OLD.snapshot_quantity IS DISTINCT FROM NEW.snapshot_quantity THEN
                            RAISE EXCEPTION 'inventory-count-membership-immutable';
                        END IF;
                        IF (OLD.actual_quantity IS DISTINCT FROM NEW.actual_quantity
                            OR OLD.difference IS DISTINCT FROM NEW.difference
                            OR OLD.variance_reason IS DISTINCT FROM NEW.variance_reason)
                           AND parent_status NOT IN ('InProgress', 'Completed') THEN
                            RAISE EXCEPTION 'inventory-count-line-terminal';
                        END IF;
                        RETURN NEW;
                    END IF;
                END;
                $$;

                CREATE TRIGGER trg_inventory_count_line_membership_guard
                BEFORE INSERT OR UPDATE OR DELETE ON public.inventory_count_lines
                FOR EACH ROW EXECUTE FUNCTION public.guard_inventory_count_line_membership();
                """);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION public.guard_inventory_count_scope_material_membership()
                RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE parent_status text;
                BEGIN
                    SELECT status INTO parent_status
                    FROM public.inventory_counts
                    WHERE id = CASE WHEN TG_OP = 'DELETE' THEN OLD.count_id ELSE NEW.count_id END
                    FOR UPDATE;

                    IF parent_status IS NULL THEN
                        RAISE EXCEPTION 'inventory-count-membership-parent-missing';
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        IF parent_status <> 'Planned' THEN
                            RAISE EXCEPTION 'inventory-count-membership-locked';
                        END IF;
                        RETURN NEW;
                    ELSIF TG_OP = 'DELETE' THEN
                        IF parent_status <> 'Planned' THEN
                            RAISE EXCEPTION 'inventory-count-membership-locked';
                        END IF;
                        RETURN OLD;
                    ELSE
                        IF OLD.count_id IS DISTINCT FROM NEW.count_id
                           OR OLD.material_id IS DISTINCT FROM NEW.material_id THEN
                            RAISE EXCEPTION 'inventory-count-membership-immutable';
                        END IF;
                        RETURN NEW;
                    END IF;
                END;
                $$;

                CREATE TRIGGER trg_inventory_count_scope_material_membership_guard
                BEFORE INSERT OR UPDATE OR DELETE ON public.inventory_count_scope_materials
                FOR EACH ROW EXECUTE FUNCTION public.guard_inventory_count_scope_material_membership();
                """);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION public.require_inventory_count_snapshot_before_start()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW.status IN ('InProgress', 'Completed', 'Closed')
                       AND NOT EXISTS (
                           SELECT 1 FROM public.inventory_count_lines AS line WHERE line.count_id = NEW.id) THEN
                        RAISE EXCEPTION 'inventory-count-snapshot-required-before-start';
                    END IF;
                    RETURN NULL;
                END;
                $$;

                CREATE CONSTRAINT TRIGGER trg_inventory_count_snapshot_required
                AFTER INSERT OR UPDATE ON public.inventory_counts
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION public.require_inventory_count_snapshot_before_start();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM public.inventory_counts WHERE status = 'Aborted') THEN
                        RAISE EXCEPTION
                            'inventory-count-abort-rollback-blocked: aborted counts exist; restore from backup or use a forward fix';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql(
                """
                DROP TRIGGER trg_inventory_count_scope_material_membership_guard ON public.inventory_count_scope_materials;
                DROP FUNCTION public.guard_inventory_count_scope_material_membership();
                DROP TRIGGER trg_inventory_count_line_membership_guard ON public.inventory_count_lines;
                DROP FUNCTION public.guard_inventory_count_line_membership();
                DROP TRIGGER trg_inventory_count_snapshot_required ON public.inventory_counts;
                DROP FUNCTION public.require_inventory_count_snapshot_before_start();
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_inventory_counts_aborted_timestamp",
                schema: "public",
                table: "inventory_counts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_inventory_counts_status_valid",
                schema: "public",
                table: "inventory_counts");

            migrationBuilder.DropColumn(
                name: "aborted_at_utc",
                schema: "public",
                table: "inventory_counts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_inventory_counts_status_valid",
                schema: "public",
                table: "inventory_counts",
                sql: "status IN ('Planned', 'InProgress', 'Completed', 'Closed')");
        }
    }
}
