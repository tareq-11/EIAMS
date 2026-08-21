using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.InventoryCounts;
using Domain.Common;
using Domain.InventoryCounts;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryCounts.ChangeStatus;

internal sealed class ChangeInventoryCountStatusCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IApplicationTransaction transaction,
    IWarehouseOperationLock warehouseOperationLock,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ChangeInventoryCountStatusCommand>
{
    public async Task<Result> Handle(ChangeInventoryCountStatusCommand command, CancellationToken cancellationToken)
    {
        Result<bool> result = await transaction.ExecuteAsync(
            async ct =>
            {
                Result inner = await HandleCoreAsync(command, ct);
                return inner.IsFailure ? Result.Failure<bool>(inner.Error) : Result.Success(true);
            },
            cancellationToken);

        return result.IsFailure ? Result.Failure(result.Error) : Result.Success();
    }

    private async Task<Result> HandleCoreAsync(ChangeInventoryCountStatusCommand command, CancellationToken cancellationToken)
    {
        InventoryCount? count = await context.InventoryCounts
            .SingleOrDefaultAsync(item => item.Id == command.CountId, cancellationToken);

        if (count is null || !await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId, PermissionCodes.InventoryCounts.Review,
            ScopeType.Warehouse, count.WarehouseId, cancellationToken))
        {
            return Result.Failure(InventoryCountErrors.NotFound(command.CountId));
        }

        if (count.RowVersion != command.ExpectedRowVersion)
        {
            return Result.Failure(InventoryCountErrors.RowVersionMismatch(
                count.Id, command.ExpectedRowVersion, count.RowVersion));
        }

        await warehouseOperationLock.AcquireAsync([count.WarehouseId], cancellationToken);

        if (command.TargetStatus == InventoryCountStatus.InProgress &&
            await context.InventoryCounts.AnyAsync(item => item.WarehouseId == count.WarehouseId &&
                item.Id != count.Id && item.Status == InventoryCountStatus.InProgress, cancellationToken))
        {
            return Result.Failure(InventoryCountErrors.AnotherCountInProgress(count.WarehouseId));
        }

        if (command.TargetStatus == InventoryCountStatus.Completed &&
            await context.InventoryCountLines.AnyAsync(item => item.CountId == count.Id && item.ActualQuantity == null, cancellationToken))
        {
            return Result.Failure(InventoryCountErrors.ActualsIncomplete(count.Id));
        }

        if (command.TargetStatus == InventoryCountStatus.Closed &&
            await context.InventoryCountLines.AnyAsync(item => item.CountId == count.Id &&
                item.Difference != 0 && item.VarianceReason == null, cancellationToken))
        {
            return Result.Failure(InventoryCountErrors.VarianceReasonsRequired(count.Id));
        }

        Result result = command.TargetStatus switch
        {
            InventoryCountStatus.InProgress => count.Start(dateTimeProvider.UtcNow),
            InventoryCountStatus.Completed => count.Complete(dateTimeProvider.UtcNow),
            InventoryCountStatus.Closed => count.Close(dateTimeProvider.UtcNow),
            _ => Result.Failure(InventoryCountErrors.InvalidTransition(count.Id, count.Status, command.TargetStatus))
        };

        if (result.IsFailure)
        {
            return result;
        }

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
        catch (DbUpdateException) when (command.TargetStatus == InventoryCountStatus.InProgress)
        {
            return Result.Failure(InventoryCountErrors.AnotherCountInProgress(count.WarehouseId));
        }
        return Result.Success();
    }
}
