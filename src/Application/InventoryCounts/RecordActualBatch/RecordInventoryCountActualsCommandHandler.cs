using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.InventoryCounts;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryCounts.RecordActualBatch;

internal sealed class RecordInventoryCountActualsCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<RecordInventoryCountActualsCommand>
{
    public async Task<Result> Handle(
        RecordInventoryCountActualsCommand command,
        CancellationToken cancellationToken)
    {
        InventoryCount? count = await context.InventoryCounts
            .SingleOrDefaultAsync(item => item.Id == command.CountId, cancellationToken);

        if (count is null || !await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.InventoryCounts.EnterActual,
            ScopeType.Warehouse,
            count.WarehouseId,
            cancellationToken))
        {
            return Result.Failure(InventoryCountErrors.NotFound(command.CountId));
        }

        if (count.RowVersion != command.ExpectedRowVersion)
        {
            return Result.Failure(InventoryCountErrors.RowVersionMismatch(
                count.Id, command.ExpectedRowVersion, count.RowVersion));
        }

        if (count.Status != InventoryCountStatus.InProgress)
        {
            return Result.Failure(InventoryCountErrors.InvalidTransition(
                count.Id, count.Status, count.Status));
        }

        Guid[] lineIds = command.Actuals.Select(item => item.LineId).ToArray();
        List<InventoryCountLine> lines = await context.InventoryCountLines
            .Where(item => item.CountId == count.Id && lineIds.Contains(item.Id))
            .ToListAsync(cancellationToken);

        if (lines.Count != lineIds.Length)
        {
            Guid missingLineId = lineIds.First(id => lines.All(line => line.Id != id));
            return Result.Failure(InventoryCountLineErrors.NotFound(missingLineId));
        }

        var linesById = lines.ToDictionary(item => item.Id);
        foreach (InventoryCountActualInput input in command.Actuals)
        {
            Result result = linesById[input.LineId].RecordActual(input.ActualQuantity);
            if (result.IsFailure)
            {
                return result;
            }
        }

        count.RegisterLineMutation();

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            int? current = await context.InventoryCounts.AsNoTracking()
                .Where(item => item.Id == count.Id)
                .Select(item => (int?)item.RowVersion)
                .SingleOrDefaultAsync(cancellationToken);
            return Result.Failure(InventoryCountErrors.RowVersionMismatch(
                count.Id, command.ExpectedRowVersion, current));
        }

        return Result.Success();
    }
}
