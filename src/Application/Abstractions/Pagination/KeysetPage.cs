namespace Application.Abstractions.Pagination;

public sealed record KeysetPage<T>(
    IReadOnlyList<T> Items,
    int PageSize,
    bool HasMore,
    DateTime? NextCreatedAtUtc,
    Guid? NextId);
