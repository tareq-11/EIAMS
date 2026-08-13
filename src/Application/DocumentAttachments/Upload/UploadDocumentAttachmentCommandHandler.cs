using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Domain.Common;
using Domain.DocumentAttachments;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.DocumentAttachments.Upload;

internal sealed class UploadDocumentAttachmentCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IFileStorage fileStorage,
    IAttachmentFileCleanup fileCleanup,
    IDatabaseExceptionClassifier databaseExceptionClassifier,
    IDateTimeProvider dateTimeProvider,
    IOptions<AttachmentStorageOptions> storageOptions)
    : ICommandHandler<UploadDocumentAttachmentCommand, Guid>
{
    public async Task<Result<Guid>> Handle(UploadDocumentAttachmentCommand command, CancellationToken cancellationToken)
    {
        WarehouseDocument? document = await context.WarehouseDocuments
            .SingleOrDefaultAsync(d => d.Id == command.DocumentId, cancellationToken);

        if (document is null)
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.NotFound(command.DocumentId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.Edit,
            ScopeType.Warehouse,
            document.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.NotFound(command.DocumentId));
        }

        if (document.RowVersion != command.ExpectedRowVersion)
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.RowVersionMismatch(
                command.DocumentId,
                command.ExpectedRowVersion,
                document.RowVersion));
        }

        if (document.DocumentStatus != DocumentStatus.Draft)
        {
            return Result.Failure<Guid>(DocumentAttachmentErrors.NotEditable);
        }

        AttachmentStorageOptions options = storageOptions.Value;

        if (command.ContentLength <= 0)
        {
            return Result.Failure<Guid>(DocumentAttachmentErrors.FileEmpty);
        }

        if (command.ContentLength > options.MaxFileSizeInBytes)
        {
            return Result.Failure<Guid>(DocumentAttachmentErrors.FileTooLarge(options.MaxFileSizeInBytes));
        }

        if (!options.IsMimeTypeAllowed(command.MimeType))
        {
            return Result.Failure<Guid>(DocumentAttachmentErrors.MimeTypeNotAllowed(command.MimeType));
        }

        if (command.AttachmentType == AttachmentType.SignedOriginal)
        {
            bool alreadyHasSignedOriginal = await context.DocumentAttachments.AnyAsync(
                a => a.DocumentId == command.DocumentId && a.AttachmentType == AttachmentType.SignedOriginal,
                cancellationToken);

            if (alreadyHasSignedOriginal)
            {
                return Result.Failure<Guid>(DocumentAttachmentErrors.SignedOriginalAlreadyExists(command.DocumentId));
            }
        }

        Result<StoredFile> storageResult = await fileStorage.SaveAsync(command.Content, cancellationToken);

        if (storageResult.IsFailure)
        {
            return Result.Failure<Guid>(storageResult.Error);
        }

        StoredFile storedFile = storageResult.Value;
        DateTime nowUtc = dateTimeProvider.UtcNow;

        var attachment = DocumentAttachment.Create(
            Guid.NewGuid(),
            command.DocumentId,
            command.AttachmentType,
            storedFile.StorageKey,
            SanitizeFilename(command.OriginalFilename),
            command.MimeType,
            storedFile.FileSize,
            storedFile.Checksum,
            userContext.UserId,
            nowUtc);

        context.DocumentAttachments.Add(attachment);

        Result detailMutationResult = command.AttachmentType == AttachmentType.SignedOriginal
            ? document.SetSignedCopy(attachment.Id)
            : document.RegisterDetailMutation();

        if (detailMutationResult.IsFailure)
        {
            await fileCleanup.DeleteOrEnqueueAsync(storedFile.StorageKey, CancellationToken.None);
            return Result.Failure<Guid>(detailMutationResult.Error);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await fileCleanup.DeleteOrEnqueueAsync(storedFile.StorageKey, CancellationToken.None);

            int? currentRowVersion = await context.WarehouseDocuments
                .AsNoTracking()
                .Where(d => d.Id == command.DocumentId)
                .Select(d => (int?)d.RowVersion)
                .SingleOrDefaultAsync(cancellationToken);

            return Result.Failure<Guid>(WarehouseDocumentErrors.RowVersionMismatch(
                command.DocumentId,
                command.ExpectedRowVersion,
                currentRowVersion));
        }
        catch (DbUpdateException exception)
        {
            // Storage succeeded but the database write failed - delete the orphaned file rather
            // than leaking it (M3-PLAN.md §1.4).
            await fileCleanup.DeleteOrEnqueueAsync(storedFile.StorageKey, CancellationToken.None);

            if (command.AttachmentType == AttachmentType.SignedOriginal &&
                databaseExceptionClassifier.IsUniqueConstraintViolation(
                    exception,
                    "ux_document_attachments_signed_original"))
            {
                return Result.Failure<Guid>(DocumentAttachmentErrors.SignedOriginalAlreadyExists(command.DocumentId));
            }

            throw;
        }

        return attachment.Id;
    }

    private static string SanitizeFilename(string filename)
    {
        string name = Path.GetFileName(filename);
        char[] invalidChars = Path.GetInvalidFileNameChars();
        Span<char> buffer = name.Length <= 300 ? stackalloc char[name.Length] : new char[300];
        int length = Math.Min(name.Length, buffer.Length);

        for (int i = 0; i < length; i++)
        {
            buffer[i] = invalidChars.Contains(name[i]) ? '_' : name[i];
        }

        return new string(buffer[..length]);
    }
}
