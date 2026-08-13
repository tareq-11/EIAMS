using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;

namespace Application.WarehouseDocuments.GetList;

public sealed record GetWarehouseDocumentsQuery(
    Guid? WarehouseId,
    Guid? SiteId,
    DocumentType? DocumentType,
    DocumentStatus? DocumentStatus,
    string? SystemReferenceNumber,
    string? PaperDocumentNumber,
    DateTime? FromDateUtc,
    DateTime? ToDateUtc,
    int Page,
    int PageSize) : IQuery<PagedResult<WarehouseDocumentResponse>>;
