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

        foreach ((Guid warehouseId, Guid materialId) in keys)
        {
            await EnsureBalanceRowExistsAsync(
                warehouseId,
                materialId,
                postedBy,
                postedAtUtc,
                cancellationToken);
        }

        var lockedBalances = new Dictionary<(Guid, Guid), InventoryBalance>();

        foreach ((Guid warehouseId, Guid materialId) in keys)
        {
            lockedBalances[(warehouseId, materialId)] =
                await LockBalanceAsync(warehouseId, materialId, cancellationToken);
        }

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

        foreach ((Guid warehouseId, Guid materialId) in keys)
        {
            decimal total = await dbContext.StockMovements
                .Where(m => m.WarehouseId == warehouseId && m.MaterialId == materialId)
                .SumAsync(m => m.QuantityDelta, cancellationToken);

            Result updateResult = lockedBalances[(warehouseId, materialId)].SetQuantity(total, postedAtUtc);

            if (updateResult.IsFailure)
            {
                return updateResult;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task EnsureBalanceRowExistsAsync(
        Guid warehouseId,
        Guid materialId,
        Guid postedBy,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
              INSERT INTO public.inventory_balances
                  (id, warehouse_id, material_id, quantity, last_updated_utc, row_version, created_at_utc, created_by)
              VALUES
                  ({Guid.NewGuid()}, {warehouseId}, {materialId}, {0m}, {nowUtc}, {1}, {nowUtc}, {postedBy})
              ON CONFLICT (warehouse_id, material_id) DO NOTHING
              """,
            cancellationToken);
    }

    private async Task<InventoryBalance> LockBalanceAsync(
        Guid warehouseId,
        Guid materialId,
        CancellationToken cancellationToken) =>
        await dbContext.InventoryBalances
            .FromSqlInterpolated(
                $"SELECT * FROM inventory_balances WHERE warehouse_id = {warehouseId} AND material_id = {materialId} FOR UPDATE")
            .SingleAsync(cancellationToken);
}
