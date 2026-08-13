using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Domain.Common;
using Domain.DocumentAttachments;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.DocumentAttachments.Remove;

internal sealed class RemoveDocumentAttachmentCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IAttachmentFileCleanup fileCleanup)
    : ICommandHandler<RemoveDocumentAttachmentCommand>
{
    public async Task<Result> Handle(RemoveDocumentAttachmentCommand command, CancellationToken cancellationToken)
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
            return Result.Failure(DocumentAttachmentErrors.NotEditable);
        }

        DocumentAttachment? attachment = await context.DocumentAttachments.SingleOrDefaultAsync(
            a => a.Id == command.AttachmentId && a.DocumentId == command.DocumentId,
            cancellationToken);

        if (attachment is null)
        {
            return Result.Failure(DocumentAttachmentErrors.NotFound(command.AttachmentId));
        }

        if (document.SignedCopyAttachmentId == attachment.Id)
        {
            Result clearResult = document.RemoveSignedCopy();

            if (clearResult.IsFailure)
            {
                return clearResult;
            }
        }
        else
        {
            Result detailMutationResult = document.RegisterDetailMutation();

            if (detailMutationResult.IsFailure)
            {
                return detailMutationResult;
            }
        }

        attachment.MarkAsRemoved();
        context.DocumentAttachments.Remove(attachment);

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

        // The database is now authoritative. If immediate storage deletion fails, a durable queue
        // retries it in the background without undoing the successful document mutation.
        await fileCleanup.DeleteOrEnqueueAsync(attachment.StorageKey, CancellationToken.None);

        return Result.Success();
    }
}
