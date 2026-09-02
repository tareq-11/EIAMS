using Application.Abstractions.Data;
using Application.Abstractions.Ledger;
using Domain.InventoryBalances;
using Domain.StockMovements;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.Ledger;

internal sealed class InventoryLedgerWriter(
    ApplicationDbContext dbContext,
    IDatabaseExceptionClassifier databaseExceptionClassifier,
    IInventoryKeyLock inventoryKeyLock) : IInventoryLedgerWriter
{
    private const string UniqueMovementConstraint =
        "ix_stock_movements_document_id_line_id_movement_type";
    private const string InitialOpeningConstraint =
        "ix_stock_movements_initial_opening_once";

    public async Task<Result> AppendAsync(
        IReadOnlyCollection<MovementDraft> movements,
        Guid postedBy,
        DateTime postedAtUtc,
        CancellationToken cancellationToken)
    {
        if (movements.Count == 0)
        {
            return Result.Success();
        }

        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Inventory ledger writes must run inside an active database transaction.");
        }

        MovementDraft? duplicateDraft = movements
            .GroupBy(movement => new
            {
                movement.DocumentId,
                movement.LineId,
                movement.MovementType
            })
            .Where(group => group.Count() > 1)
            .Select(group => group.First())
            .FirstOrDefault();

        if (duplicateDraft is not null)
        {
            return Result.Failure(StockMovementErrors.DuplicatePosting(
                duplicateDraft.DocumentId,
                duplicateDraft.LineId));
        }

        var keys = movements
            .Select(m => (m.WarehouseId, m.MaterialId))
            .Distinct()
            .OrderBy(k => k.WarehouseId)
            .ThenBy(k => k.MaterialId)
            .ToList();

        await inventoryKeyLock.AcquireAsync(keys, cancellationToken);

        await EnsureBalancesExistAsync(keys, postedBy, postedAtUtc, cancellationToken);

        Guid[] warehouseIds = keys.Select(k => k.WarehouseId).ToArray();
        Guid[] materialIds = keys.Select(k => k.MaterialId).ToArray();

        List<InventoryBalance> balances = await dbContext.InventoryBalances
            .FromSqlInterpolated($$"""
                SELECT balance.*
                FROM public.inventory_balances AS balance
                INNER JOIN unnest({{warehouseIds}}, {{materialIds}})
                    AS requested(warehouse_id, material_id)
                    ON requested.warehouse_id = balance.warehouse_id
                    AND requested.material_id = balance.material_id
                FOR UPDATE OF balance
                """)
            .ToListAsync(cancellationToken);

        var lockedBalances = balances.ToDictionary(b => (b.WarehouseId, b.MaterialId));

        foreach (IGrouping<(Guid WarehouseId, Guid MaterialId), MovementDraft> movementGroup in movements
                     .GroupBy(movement => (movement.WarehouseId, movement.MaterialId)))
        {
            decimal netDelta = movementGroup.Sum(movement => movement.QuantityDelta);
            InventoryBalance balance = lockedBalances[movementGroup.Key];

            if (netDelta < 0 && balance.Quantity + netDelta < 0)
            {
                return Result.Failure(InventoryBalanceErrors.InsufficientQuantity(
                    movementGroup.Key.WarehouseId,
                    movementGroup.Key.MaterialId,
                    balance.Quantity,
                    -netDelta));
            }
        }

        foreach (MovementDraft draft in movements)
        {
            Result<StockMovement> movementResult = StockMovement.Create(
                Guid.NewGuid(),
                draft.WarehouseId,
                draft.MaterialId,
                draft.DocumentId,
                draft.LineId,
                draft.MovementType,
                draft.QuantityDelta,
                postedBy,
                postedAtUtc);

            if (movementResult.IsFailure)
            {
                return Result.Failure(movementResult.Error);
            }

            dbContext.StockMovements.Add(movementResult.Value);
        }

        // Flush the inserted movements first so the SUM below - inside the same transaction - sees
        // them (D-MOV-01/D-INV-01: the balance is always recomputed from the ledger, never trusted
        // as an in-memory increment).
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (databaseExceptionClassifier.IsUniqueConstraintViolation(
            exception,
            InitialOpeningConstraint))
        {
            MovementDraft openingMovement = movements.First(movement =>
                movement.MovementType == Domain.Common.MovementType.Opening &&
                movement.QuantityDelta > 0);

            return Result.Failure(Domain.WarehouseDocuments.OpeningDocumentErrors.AlreadyInitialized(
                openingMovement.WarehouseId,
                openingMovement.MaterialId));
        }
        catch (DbUpdateException exception) when (databaseExceptionClassifier.IsUniqueConstraintViolation(
            exception,
            UniqueMovementConstraint))
        {
            return Result.Failure(StockMovementErrors.DuplicatePosting(
                movements.First().DocumentId));
        }

        List<InventoryTotalRow> totals = await dbContext.Database.SqlQuery<InventoryTotalRow>($$"""
                SELECT requested.warehouse_id,
                       requested.material_id,
                       COALESCE(SUM(movement.quantity_delta), 0) AS total
                FROM unnest({{warehouseIds}}, {{materialIds}})
                    AS requested(warehouse_id, material_id)
                LEFT JOIN public.stock_movements AS movement
                    ON movement.warehouse_id = requested.warehouse_id
                    AND movement.material_id = requested.material_id
                GROUP BY requested.warehouse_id, requested.material_id
                """)
            .ToListAsync(cancellationToken);

        var totalsMap = totals.ToDictionary(x => (x.WarehouseId, x.MaterialId), x => x.Total);

        foreach ((Guid warehouseId, Guid materialId) in keys)
        {
            decimal total = totalsMap.TryGetValue((warehouseId, materialId), out decimal sum) ? sum : 0m;

            Result updateResult = lockedBalances[(warehouseId, materialId)].SetQuantity(total, postedAtUtc);

            if (updateResult.IsFailure)
            {
                return updateResult;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task EnsureBalancesExistAsync(
        IReadOnlyList<(Guid WarehouseId, Guid MaterialId)> keys,
        Guid postedBy,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        Guid[] balanceIds = keys.Select(_ => Guid.NewGuid()).ToArray();
        Guid[] warehouseIds = keys.Select(key => key.WarehouseId).ToArray();
        Guid[] materialIds = keys.Select(key => key.MaterialId).ToArray();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $$"""
              INSERT INTO public.inventory_balances
                  (id, warehouse_id, material_id, quantity, last_updated_utc, row_version, created_at_utc, created_by)
              SELECT requested.id,
                     requested.warehouse_id,
                     requested.material_id,
                     {{0m}},
                     {{nowUtc}},
                     {{1}},
                     {{nowUtc}},
                     {{postedBy}}
              FROM unnest({{balanceIds}}, {{warehouseIds}}, {{materialIds}})
                  AS requested(id, warehouse_id, material_id)
              ON CONFLICT (warehouse_id, material_id) DO NOTHING
              """,
            cancellationToken);
    }

    private sealed record InventoryTotalRow(Guid WarehouseId, Guid MaterialId, decimal Total);
}
