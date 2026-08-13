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

namespace Application.DocumentAttachments.GetContent;

internal sealed class GetDocumentAttachmentContentQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IFileStorage fileStorage)
    : IQueryHandler<GetDocumentAttachmentContentQuery, DocumentAttachmentContentResponse>
{
    public async Task<Result<DocumentAttachmentContentResponse>> Handle(
        GetDocumentAttachmentContentQuery query,
        CancellationToken cancellationToken)
    {
        WarehouseDocument? document = await context.WarehouseDocuments
            .SingleOrDefaultAsync(d => d.Id == query.DocumentId, cancellationToken);

        if (document is null)
        {
            return Result.Failure<DocumentAttachmentContentResponse>(WarehouseDocumentErrors.NotFound(query.DocumentId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.View,
            ScopeType.Warehouse,
            document.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<DocumentAttachmentContentResponse>(WarehouseDocumentErrors.NotFound(query.DocumentId));
        }

        DocumentAttachment? attachment = await context.DocumentAttachments.SingleOrDefaultAsync(
            a => a.Id == query.AttachmentId && a.DocumentId == query.DocumentId,
            cancellationToken);

        if (attachment is null)
        {
            return Result.Failure<DocumentAttachmentContentResponse>(DocumentAttachmentErrors.NotFound(query.AttachmentId));
        }

        Result<Stream> contentResult = await fileStorage.OpenAsync(attachment.StorageKey, cancellationToken);

        if (contentResult.IsFailure)
        {
            return Result.Failure<DocumentAttachmentContentResponse>(contentResult.Error);
        }

        return new DocumentAttachmentContentResponse
        {
            Content = contentResult.Value,
            MimeType = attachment.MimeType,
            OriginalFilename = attachment.OriginalFilename
        };
    }
}
