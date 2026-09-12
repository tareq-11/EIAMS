using Infrastructure.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912000009_MIG_COUNT_01_Orthogonal_Count_State")]
public sealed class MIG_COUNT_01_Orthogonal_Count_State : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "aborted_at_utc",
            table: "inventory_counts",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "aborted_by",
            table: "inventory_counts",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "abort_reason",
            table: "inventory_counts",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.Sql(@"
            ALTER TABLE public.inventory_counts
            DROP CONSTRAINT IF EXISTS ck_inventory_counts_status_valid;
            ALTER TABLE public.inventory_counts
            ADD CONSTRAINT ck_inventory_counts_status_valid
            CHECK (status IN ('Planned', 'InProgress', 'Completed', 'Closed', 'Aborted'))
            NOT VALID;
            ALTER TABLE public.inventory_counts
            ADD CONSTRAINT ck_inventory_counts_aborted_audit
            CHECK (status <> 'Aborted' OR
                   (aborted_at_utc IS NOT NULL AND aborted_by IS NOT NULL
                    AND abort_reason IS NOT NULL AND btrim(abort_reason) <> ''))
            NOT VALID;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            ALTER TABLE public.inventory_counts DROP CONSTRAINT IF EXISTS ck_inventory_counts_aborted_audit;
            ALTER TABLE public.inventory_counts DROP CONSTRAINT IF EXISTS ck_inventory_counts_status_valid;
            ALTER TABLE public.inventory_counts
            ADD CONSTRAINT ck_inventory_counts_status_valid
            CHECK (status IN ('Planned', 'InProgress', 'Completed', 'Closed'));
        ");
        migrationBuilder.DropColumn(name: "abort_reason", table: "inventory_counts");
        migrationBuilder.DropColumn(name: "aborted_by", table: "inventory_counts");
        migrationBuilder.DropColumn(name: "aborted_at_utc", table: "inventory_counts");
    }
}
