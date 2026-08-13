using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.DocumentLines;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.DocumentLines.Remove;

internal sealed class RemoveDocumentLineCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<RemoveDocumentLineCommand>
{
    public async Task<Result> Handle(RemoveDocumentLineCommand command, CancellationToken cancellationToken)
    {
        WarehouseDocument? document = await context.WarehouseDocuments
            .SingleOrDefaultAsync(d => d.Id == command.DocumentId, cancellationToken);

        if (document is null)
        {
            return Result.Failure(WarehouseDocumentErrors.NotFound(command.DocumentId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.Edit,
            ScopeType.Warehouse,
            document.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure(WarehouseDocumentErrors.NotFound(command.DocumentId));
        }

        if (document.RowVersion != command.ExpectedRowVersion)
        {
            return Result.Failure(WarehouseDocumentErrors.RowVersionMismatch(
                command.DocumentId,
                command.ExpectedRowVersion,
                document.RowVersion));
        }

        if (document.DocumentStatus != DocumentStatus.Draft)
        {
            return Result.Failure(WarehouseDocumentErrors.NotEditable(command.DocumentId, document.DocumentStatus));
        }

        if (document.ReversalOfDocumentId is not null)
        {
            return Result.Failure(WarehouseDocumentErrors.ReversalLinesImmutable(document.Id));
        }

        DocumentLine? line = await context.DocumentLines.SingleOrDefaultAsync(
            l => l.Id == command.LineId && l.DocumentId == command.DocumentId,
            cancellationToken);

        if (line is null)
        {
            return Result.Failure(DocumentLineErrors.NotFound(command.LineId));
        }

        line.MarkAsRemoved();
        context.DocumentLines.Remove(line);

        Result detailMutationResult = document.RegisterDetailMutation();

        if (detailMutationResult.IsFailure)
        {
            return detailMutationResult;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            int? currentRowVersion = await context.WarehouseDocuments
                .AsNoTracking()
                .Where(d => d.Id == command.DocumentId)
                .Select(d => (int?)d.RowVersion)
                .SingleOrDefaultAsync(cancellationToken);

            return Result.Failure(WarehouseDocumentErrors.RowVersionMismatch(
                command.DocumentId,
                command.ExpectedRowVersion,
                currentRowVersion));
        }

        return Result.Success();
    }
}
