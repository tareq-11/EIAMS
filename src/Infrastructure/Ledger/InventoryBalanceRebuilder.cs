using Application.Abstractions.Data;
using Application.Abstractions.Ledger;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.Ledger;

internal sealed class InventoryBalanceRebuilder(
    ApplicationDbContext dbContext,
    IApplicationTransaction transaction) : IInventoryBalanceRebuilder
{
    public Task<Result<int>> RebuildAllAsync(
        Guid rebuiltBy,
        DateTime rebuiltAtUtc,
        CancellationToken cancellationToken)
    {
        if (rebuiltBy == Guid.Empty)
        {
            return Task.FromResult(Result.Failure<int>(Error.Failure(
                "InventoryBalances.RebuildActorRequired",
                "A valid actor is required to audit a balance-cache rebuild.")));
        }

        if (rebuiltAtUtc.Kind != DateTimeKind.Utc)
        {
            return Task.FromResult(Result.Failure<int>(Error.Failure(
                "InventoryBalances.RebuildTimeMustBeUtc",
                "The cache rebuild timestamp must be UTC.")));
        }

        return transaction.ExecuteAsync(async ct =>
        {
            // Use the same relation-lock order as posting: balance cache first, then immutable
            // movements. Posting can create a missing cache row before inserting its ledger fact;
            // reversing this order here could deadlock a writer holding the cache relation lock.
            // These locks wait for in-flight writers and prevent new writes until this transaction
            // commits. No posted facts are rewritten.
            await dbContext.Database.ExecuteSqlRawAsync(
                "LOCK TABLE public.inventory_balances IN SHARE ROW EXCLUSIVE MODE",
                ct);
            await dbContext.Database.ExecuteSqlRawAsync(
                "LOCK TABLE public.stock_movements IN SHARE MODE",
                ct);

            int affectedRows = await dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
                INSERT INTO public.inventory_balances
                    (id, warehouse_id, material_id, quantity, last_updated_utc, row_version,
                     created_at_utc, updated_at_utc, created_by, updated_by)
                SELECT gen_random_uuid(), keys.warehouse_id, keys.material_id,
                       COALESCE(SUM(movement.quantity_delta), 0), {{rebuiltAtUtc}}, 1,
                       {{rebuiltAtUtc}}, NULL, {{rebuiltBy}}, NULL
                FROM (
                    SELECT warehouse_id, material_id FROM public.inventory_balances
                    UNION
                    SELECT warehouse_id, material_id FROM public.stock_movements
                ) AS keys
                LEFT JOIN public.stock_movements AS movement
                    ON movement.warehouse_id = keys.warehouse_id
                    AND movement.material_id = keys.material_id
                GROUP BY keys.warehouse_id, keys.material_id
                ON CONFLICT (warehouse_id, material_id) DO UPDATE
                SET quantity = EXCLUDED.quantity,
                    last_updated_utc = EXCLUDED.last_updated_utc,
                    row_version = inventory_balances.row_version + 1,
                    updated_at_utc = EXCLUDED.last_updated_utc,
                    updated_by = EXCLUDED.created_by
                """, ct);

            return Result.Success(affectedRows);
        }, cancellationToken);
    }
}
