using Infrastructure.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912000012_MIG_OPS_01_Backfill_Checkpoints")]
public sealed class MIG_OPS_01_Backfill_Checkpoints : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "migration_backfill_runs",
            schema: "public",
            columns: table => new
            {
                family = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                input_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                high_water_mark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                processed_count = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                failed_count = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                application_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                migration_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                last_error = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_migration_backfill_runs", x => x.family);
                table.CheckConstraint("ck_migration_backfill_runs_status", "status IN ('Running', 'Completed', 'Blocked', 'Failed')");
                table.CheckConstraint("ck_migration_backfill_runs_counts", "processed_count >= 0 AND failed_count >= 0");
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "migration_backfill_runs", schema: "public");
}
