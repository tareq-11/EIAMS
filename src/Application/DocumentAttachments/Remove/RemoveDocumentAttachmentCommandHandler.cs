using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.DocumentAttachments.GetList;
using Domain.Common;
using Domain.DocumentAttachments;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.DocumentAttachments.Remove;

internal sealed class RemoveDocumentAttachmentCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IAttachmentFileCleanup fileCleanup,
    IOptions<AttachmentMalwareScanOptions> malwareScanOptions)
    : ICommandHandler<RemoveDocumentAttachmentCommand, AttachmentMutationResponse>
{
    public async Task<Result<AttachmentMutationResponse>> Handle(
        RemoveDocumentAttachmentCommand command,
        CancellationToken cancellationToken)
    {
        WarehouseDocument? document = await context.WarehouseDocuments
            .SingleOrDefaultAsync(d => d.Id == command.DocumentId, cancellationToken);

        if (document is null)
        {
            return Result.Failure<AttachmentMutationResponse>(WarehouseDocumentErrors.NotFound(command.DocumentId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.Edit,
            ScopeType.Warehouse,
            document.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<AttachmentMutationResponse>(WarehouseDocumentErrors.NotFound(command.DocumentId));
        }

        if (document.RowVersion != command.ExpectedRowVersion)
        {
            return Result.Failure<AttachmentMutationResponse>(WarehouseDocumentErrors.RowVersionMismatch(
                command.DocumentId,
                command.ExpectedRowVersion,
                document.RowVersion));
        }

        if (document.DocumentStatus != DocumentStatus.Draft)
        {
            return Result.Failure<AttachmentMutationResponse>(DocumentAttachmentErrors.NotEditable);
        }

        DocumentAttachment? attachment = await context.DocumentAttachments.SingleOrDefaultAsync(
            a => a.Id == command.AttachmentId && a.DocumentId == command.DocumentId,
            cancellationToken);

        if (attachment is null)
        {
            return Result.Failure<AttachmentMutationResponse>(DocumentAttachmentErrors.NotFound(command.AttachmentId));
        }

        if (!attachment.IsActive)
        {
            return Result.Failure<AttachmentMutationResponse>(DocumentAttachmentErrors.ArchivedCannotBeRemoved(attachment.Id));
        }

        if (document.SignedCopyAttachmentId == attachment.Id)
        {
            Result clearResult = document.RemoveSignedCopy();

            if (clearResult.IsFailure)
            {
                return Result.Failure<AttachmentMutationResponse>(clearResult.Error);
            }
        }
        else
        {
            Result detailMutationResult = document.RegisterDetailMutation();

            if (detailMutationResult.IsFailure)
            {
                return Result.Failure<AttachmentMutationResponse>(detailMutationResult.Error);
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

            return Result.Failure<AttachmentMutationResponse>(WarehouseDocumentErrors.RowVersionMismatch(
                command.DocumentId,
                command.ExpectedRowVersion,
                currentRowVersion));
        }

        // The database is now authoritative. If immediate storage deletion fails, a durable queue
        // retries it in the background without undoing the successful document mutation.
        await fileCleanup.DeleteOrEnqueueAsync(attachment.StorageKey, CancellationToken.None);

        var attachmentResponse = new DocumentAttachmentResponse(
            attachment.Id,
            attachment.AttachmentType.ToString(),
            attachment.OriginalFilename,
            attachment.MimeType,
            attachment.FileSize,
            attachment.Checksum,
            attachment.UploadedBy,
            attachment.UploadedAtUtc,
            IsActive: false,
            attachment.ArchivedAtUtc,
            attachment.ArchivedBy,
            attachment.ReplacesAttachmentId,
            ReplacedByAttachmentId: null);

        return new AttachmentMutationResponse(
            attachment.Id,
            Removed: true,
            document.RowVersion,
            malwareScanOptions.Value.Policy.ToString(),
            attachment.MalwareScanClean,
            attachmentResponse);
    }
}
