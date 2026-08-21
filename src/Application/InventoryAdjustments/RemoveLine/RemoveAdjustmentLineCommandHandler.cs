using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.DocumentLines;
using Domain.InventoryAdjustments;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryAdjustments.RemoveLine;

internal sealed class RemoveAdjustmentLineCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<RemoveAdjustmentLineCommand>
{
    public async Task<Result> Handle(RemoveAdjustmentLineCommand command, CancellationToken cancellationToken)
    {
        WarehouseDocument? document = await context.WarehouseDocuments.SingleOrDefaultAsync(
            item => item.Id == command.DocumentId, cancellationToken);
        if (document is null || !await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId, PermissionCodes.WarehouseDocuments.Edit,
            ScopeType.Warehouse, document.WarehouseId, cancellationToken))
        {
            return Result.Failure(WarehouseDocumentErrors.NotFound(command.DocumentId));
        }

        Result guard = await AdjustmentLineMutationGuard.ValidateAsync(
            context, document, command.ExpectedRowVersion, cancellationToken);
        if (guard.IsFailure)
        {
            return guard;
        }

        DocumentLine? line = await context.DocumentLines.SingleOrDefaultAsync(
            item => item.Id == command.LineId && item.DocumentId == document.Id, cancellationToken);
        AdjustmentLine? adjustmentLine = await context.AdjustmentLines.SingleOrDefaultAsync(
            item => item.Id == command.LineId && item.AdjustmentId == document.Id, cancellationToken);
        if (line is null || adjustmentLine is null)
        {
            return Result.Failure(AdjustmentLineErrors.NotFound(command.LineId));
        }

        adjustmentLine.MarkAsRemoved();
        line.MarkAsRemoved();
        context.AdjustmentLines.Remove(adjustmentLine);
        context.DocumentLines.Remove(line);
        Result mutation = document.RegisterDetailMutation();
        if (mutation.IsFailure)
        {
            return mutation;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(await AdjustmentLineMutationGuard.RowVersionErrorAsync(
                context, document.Id, command.ExpectedRowVersion, cancellationToken));
        }

        return Result.Success();
    }
}
