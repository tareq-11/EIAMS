using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.DocumentAttachments.GetList;

internal sealed class GetDocumentAttachmentsQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetDocumentAttachmentsQuery, List<DocumentAttachmentResponse>>
{
    public async Task<Result<List<DocumentAttachmentResponse>>> Handle(
        GetDocumentAttachmentsQuery query,
        CancellationToken cancellationToken)
    {
        var document = await context.WarehouseDocuments
            .AsNoTracking()
            .Where(item => item.Id == query.DocumentId)
            .Select(item => new { item.WarehouseId })
            .SingleOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            return Result.Failure<List<DocumentAttachmentResponse>>(
                WarehouseDocumentErrors.NotFound(query.DocumentId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.View,
            ScopeType.Warehouse,
            document.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<List<DocumentAttachmentResponse>>(
                WarehouseDocumentErrors.NotFound(query.DocumentId));
        }

        List<DocumentAttachmentResponse> attachments = await context.DocumentAttachments
            .AsNoTracking()
            .Where(item => item.DocumentId == query.DocumentId &&
                           (query.IncludeArchived || item.IsActive))
            .OrderByDescending(item => item.IsActive)
            .ThenByDescending(item => item.UploadedAtUtc)
            .ThenByDescending(item => item.Id)
            .Select(item => new DocumentAttachmentResponse(
                item.Id,
                item.AttachmentType.ToString(),
                item.OriginalFilename,
                item.MimeType,
                item.FileSize,
                item.Checksum,
                item.UploadedBy,
                item.UploadedAtUtc,
                item.IsActive,
                item.ArchivedAtUtc,
                item.ArchivedBy,
                item.ReplacesAttachmentId,
                context.DocumentAttachments
                    .Where(replacement => replacement.ReplacesAttachmentId == item.Id)
                    .Select(replacement => (Guid?)replacement.Id)
                    .SingleOrDefault()))
            .ToListAsync(cancellationToken);

        return attachments;
    }
}
