using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseDocuments.GetList;

internal sealed class GetWarehouseDocumentsQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetWarehouseDocumentsQuery, PagedResult<WarehouseDocumentResponse>>
{
    public async Task<Result<PagedResult<WarehouseDocumentResponse>>> Handle(
        GetWarehouseDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        ScopeType requiredScopeType;
        Guid? requiredScopeId;

        if (query.WarehouseId is not null)
        {
            requiredScopeType = ScopeType.Warehouse;
            requiredScopeId = query.WarehouseId;
        }
        else if (query.SiteId is not null)
        {
            requiredScopeType = ScopeType.Site;
            requiredScopeId = query.SiteId;
        }
        else
        {
            requiredScopeType = ScopeType.Enterprise;
            requiredScopeId = null;
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.View,
            requiredScopeType,
            requiredScopeId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<PagedResult<WarehouseDocumentResponse>>(WarehouseDocumentErrors.Forbidden);
        }

        PagedResult<WarehouseDocumentResponse> documents = await (
                from document in context.WarehouseDocuments
                join warehouse in context.Warehouses on document.WarehouseId equals warehouse.Id
                where query.WarehouseId == null || document.WarehouseId == query.WarehouseId
                where query.SiteId == null || warehouse.SiteId == query.SiteId
                where query.DocumentType == null || document.DocumentType == query.DocumentType
                where query.DocumentStatus == null || document.DocumentStatus == query.DocumentStatus
                where query.SystemReferenceNumber == null ||
                      document.SystemReferenceNumber.Contains(query.SystemReferenceNumber)
                where query.PaperDocumentNumber == null ||
                      document.PaperDocumentNumber != null &&
                      document.PaperDocumentNumber.Contains(query.PaperDocumentNumber)
                where query.FromDateUtc == null || document.CreatedAtUtc >= query.FromDateUtc
                where query.ToDateUtc == null || document.CreatedAtUtc <= query.ToDateUtc
                select new WarehouseDocumentResponse
                {
                    Id = document.Id,
                    WarehouseId = document.WarehouseId,
                    DocumentType = document.DocumentType.ToString(),
                    PaperDocumentNumber = document.PaperDocumentNumber,
                    PaperDocumentYear = document.PaperDocumentYear,
                    SystemReferenceNumber = document.SystemReferenceNumber,
                    DocumentStatus = document.DocumentStatus.ToString(),
                    PostedAtUtc = document.PostedAtUtc,
                    ReversalOfDocumentId = document.ReversalOfDocumentId,
                    RowVersion = document.RowVersion,
                    CreatedAtUtc = document.CreatedAtUtc
                })
            .OrderByDescending(d => d.CreatedAtUtc)
            .ThenBy(d => d.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return documents;
    }
}
