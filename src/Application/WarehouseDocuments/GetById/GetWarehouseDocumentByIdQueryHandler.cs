using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseDocuments.GetById;

internal sealed class GetWarehouseDocumentByIdQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetWarehouseDocumentByIdQuery, WarehouseDocumentDetailsResponse>
{
    public async Task<Result<WarehouseDocumentDetailsResponse>> Handle(
        GetWarehouseDocumentByIdQuery query,
        CancellationToken cancellationToken)
    {
        WarehouseDocumentDetailsResponse? document = await context.WarehouseDocuments
            .Where(d => d.Id == query.DocumentId)
            .Select(d => new WarehouseDocumentDetailsResponse
            {
                Id = d.Id,
                WarehouseId = d.WarehouseId,
                DocumentType = d.DocumentType.ToString(),
                PaperDocumentNumber = d.PaperDocumentNumber,
                PaperDocumentYear = d.PaperDocumentYear,
                SystemReferenceNumber = d.SystemReferenceNumber,
                SignedCopyAttachmentId = d.SignedCopyAttachmentId,
                DocumentStatus = d.DocumentStatus.ToString(),
                PostedBy = d.PostedBy,
                PostedAtUtc = d.PostedAtUtc,
                ReversalOfDocumentId = d.ReversalOfDocumentId,
                RowVersion = d.RowVersion
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (document is null)
        {
            return Result.Failure<WarehouseDocumentDetailsResponse>(WarehouseDocumentErrors.NotFound(query.DocumentId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.View,
            ScopeType.Warehouse,
            document.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<WarehouseDocumentDetailsResponse>(WarehouseDocumentErrors.NotFound(query.DocumentId));
        }

        document.ReversedByDocumentId = await context.WarehouseDocuments
            .Where(d => d.ReversalOfDocumentId == query.DocumentId)
            .Select(d => (Guid?)d.Id)
            .SingleOrDefaultAsync(cancellationToken);

        document.Lines = await context.DocumentLines
            .Where(l => l.DocumentId == query.DocumentId)
            .Select(l => new DocumentLineResponse
            {
                Id = l.Id,
                SourceLineId = l.SourceLineId,
                MaterialId = l.MaterialId,
                LineType = l.LineType.ToString(),
                Quantity = l.Quantity,
                UnitId = l.UnitId,
                BaseQuantity = l.BaseQuantity,
                UnitPrice = l.UnitPrice,
                BatchNumber = l.BatchNumber,
                ExpiryDate = l.ExpiryDate
            })
            .ToListAsync(cancellationToken);

        document.Attachments = await context.DocumentAttachments
            .Where(a => a.DocumentId == query.DocumentId)
            .Select(a => new DocumentAttachmentResponse
            {
                Id = a.Id,
                AttachmentType = a.AttachmentType.ToString(),
                OriginalFilename = a.OriginalFilename,
                MimeType = a.MimeType,
                FileSize = a.FileSize,
                UploadedAtUtc = a.UploadedAtUtc
            })
            .ToListAsync(cancellationToken);

        return document;
    }
}
