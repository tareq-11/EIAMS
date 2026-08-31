using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.Reports.CountAdjustments;

public sealed record GetCountAdjustmentsReportQuery(
    Guid? WarehouseId,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int Page,
    int PageSize) : IQuery<PagedResult<CountAdjustmentsReportRow>>;
