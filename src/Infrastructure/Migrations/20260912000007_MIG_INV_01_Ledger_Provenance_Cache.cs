using Infrastructure.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260912000007_MIG_INV_01_Ledger_Provenance_Cache")]
public sealed class MIG_INV_01_Ledger_Provenance_Cache : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "idempotency_key",
            table: "stock_movements",
            type: "uuid",
            nullable: true);

        migrationBuilder.Sql(@"
            CREATE INDEX ix_stock_movements_idempotency
            ON public.stock_movements (idempotency_key)
            WHERE idempotency_key IS NOT NULL;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS public.ix_stock_movements_idempotency;");
        migrationBuilder.DropColumn(name: "idempotency_key", table: "stock_movements");
    }
}
