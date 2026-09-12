using System.Text.Json.Serialization;

namespace Web.Api.Infrastructure;

public sealed record ApiResponse<T>(
    bool Success,
    T? Data,
    ApiResponseMeta Meta);

public sealed record ApiResponseMeta(
    [property: JsonPropertyName("request_id")] string RequestId,
    [property: JsonPropertyName("timestamp_utc")] DateTime TimestampUtc);

/// <summary>
/// Paged data wrapper per target contract: PagedData&lt;T&gt; = { items: T[], pageInfo: PageInfo }.
/// </summary>
public sealed record PagedData<T>(
    [property: JsonPropertyName("items")] IReadOnlyList<T> Items,
    [property: JsonPropertyName("page_info")] PageInfo PageInfo);

public sealed record PageInfo(
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("page_size")] int PageSize,
    [property: JsonPropertyName("total_items")] int TotalItems,
    [property: JsonPropertyName("total_pages")] int TotalPages,
    [property: JsonPropertyName("has_previous_page")] bool HasPreviousPage,
    [property: JsonPropertyName("has_next_page")] bool HasNextPage);

/// <summary>
/// Keyset/cursor page info for feeds that use opaque cursors.
/// </summary>
public sealed record KeysetPageInfo(
    [property: JsonPropertyName("page_size")] int PageSize,
    [property: JsonPropertyName("has_next_page")] bool HasNextPage,
    [property: JsonPropertyName("mode")] string Mode = "cursor",
    [property: JsonPropertyName("next_created_at_utc"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTime? NextCreatedAtUtc = null,
    [property: JsonPropertyName("next_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? NextId = null);

/// <summary>
/// Paged data wrapper for cursor-based feeds.
/// </summary>
public sealed record KeysetPagedData<T>(
    [property: JsonPropertyName("items")] IReadOnlyList<T> Items,
    [property: JsonPropertyName("page_info")] KeysetPageInfo PageInfo);

public sealed record ResourceIdResponse(Guid Id);

public sealed record EmptyResponse;

public sealed record ApiErrorResponse(bool Success, ApiError Error, ApiResponseMeta Meta);

public sealed record ApiError(
    string Code,
    string Message,
    object Details);
