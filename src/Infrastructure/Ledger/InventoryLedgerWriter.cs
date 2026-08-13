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
    IDatabaseExceptionClassifier databaseExceptionClassifier) : IInventoryLedgerWriter
{
    private const string UniqueMovementConstraint =
        "ix_stock_movements_document_id_line_id_movement_type";

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

        foreach ((Guid warehouseId, Guid materialId) in keys)
        {
            await AcquireBalanceCreationLockAsync(warehouseId, materialId, cancellationToken);
        }

        foreach ((Guid warehouseId, Guid materialId) in keys)
        {
            await EnsureBalanceRowExistsAsync(warehouseId, materialId, postedAtUtc, cancellationToken);
        }

        var lockedBalances = new Dictionary<(Guid, Guid), InventoryBalance>();

        foreach ((Guid warehouseId, Guid materialId) in keys)
        {
            lockedBalances[(warehouseId, materialId)] =
                await LockBalanceAsync(warehouseId, materialId, cancellationToken);
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
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        bool exists = await dbContext.InventoryBalances.AnyAsync(
            balance => balance.WarehouseId == warehouseId && balance.MaterialId == materialId,
            cancellationToken);

        if (exists)
        {
            return;
        }

        var balance = InventoryBalance.CreateZero(
            Guid.NewGuid(),
            warehouseId,
            materialId,
            nowUtc);

        dbContext.InventoryBalances.Add(balance);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task<int> AcquireBalanceCreationLockAsync(
        Guid warehouseId,
        Guid materialId,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({warehouseId + ":" + materialId}, 0))",
            cancellationToken);

    private async Task<InventoryBalance> LockBalanceAsync(
        Guid warehouseId,
        Guid materialId,
        CancellationToken cancellationToken) =>
        await dbContext.InventoryBalances
            .FromSqlInterpolated(
                $"SELECT * FROM inventory_balances WHERE warehouse_id = {warehouseId} AND material_id = {materialId} FOR UPDATE")
            .SingleAsync(cancellationToken);
}
