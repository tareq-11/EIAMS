using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;

namespace Application.Reports.Documents;

public sealed record GetDocumentsReportQuery(
    Guid? WarehouseId,
    DocumentType? DocumentType,
    DocumentStatus? Status,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int Page,
    int PageSize) : IQuery<PagedResult<DocumentsReportRow>>;
