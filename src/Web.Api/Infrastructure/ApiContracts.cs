using System.Text.Json.Serialization;

namespace Web.Api.Infrastructure;

public sealed record ApiResponse<T>(
    bool Success,
    T? Data,
    ApiPagination? Pagination,
    ApiResponseMeta Meta);

public sealed record ApiPagination(
    int Page,
    [property: JsonPropertyName("page_size")] int PageSize,
    [property: JsonPropertyName("total_items")] int TotalItems,
    [property: JsonPropertyName("total_pages")] int TotalPages,
    [property: JsonPropertyName("has_previous_page")] bool HasPreviousPage = false,
    [property: JsonPropertyName("has_next_page")] bool HasNextPage = false,
    [property: JsonPropertyName("total_count")] int? TotalCount = null);

public sealed record ApiResponseMeta(
    [property: JsonPropertyName("request_id")] string RequestId,
    DateTime Timestamp);

public sealed record ResourceIdResponse(Guid Id);

public sealed record EmptyResponse;

public sealed record ApiErrorResponse(bool Success, ApiError Error);

public sealed record ApiError(
    string Code,
    string Message,
    object Details,
    [property: JsonPropertyName("request_id")] string RequestId);
