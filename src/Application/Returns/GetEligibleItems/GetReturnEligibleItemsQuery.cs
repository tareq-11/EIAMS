using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.Returns.GetEligibleItems;

public sealed record GetReturnEligibleItemsQuery(
    Guid OriginalIssueDocumentId,
    int Page = PaginationDefaults.DefaultPage,
    int PageSize = PaginationDefaults.DefaultPageSize) : IQuery<PagedResult<ReturnEligibleItemResponse>>;
