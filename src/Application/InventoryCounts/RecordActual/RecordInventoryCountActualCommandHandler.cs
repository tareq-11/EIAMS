using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.InventoryCounts;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryCounts.RecordActual;

internal sealed class RecordInventoryCountActualCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<RecordInventoryCountActualCommand>
{
    public async Task<Result> Handle(RecordInventoryCountActualCommand command, CancellationToken cancellationToken)
    {
        InventoryCount? count = await context.InventoryCounts.SingleOrDefaultAsync(
            item => item.Id == command.CountId, cancellationToken);
        if (count is null || !await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId, PermissionCodes.InventoryCounts.EnterActual,
            ScopeType.Warehouse, count.WarehouseId, cancellationToken))
        {
            return Result.Failure(InventoryCountErrors.NotFound(command.CountId));
        }

        if (count.RowVersion != command.ExpectedRowVersion)
        {
            return Result.Failure(InventoryCountErrors.RowVersionMismatch(count.Id, command.ExpectedRowVersion, count.RowVersion));
        }

        if (count.Status != InventoryCountStatus.InProgress)
        {
            return Result.Failure(InventoryCountErrors.InvalidTransition(count.Id, count.Status, count.Status));
        }

        InventoryCountLine? line = await context.InventoryCountLines.SingleOrDefaultAsync(
            item => item.Id == command.LineId && item.CountId == count.Id, cancellationToken);
        if (line is null)
        {
            return Result.Failure(InventoryCountLineErrors.NotFound(command.LineId));
        }

        Result result = line.RecordActual(command.ActualQuantity);
        if (result.IsFailure)
        {
            return result;
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
