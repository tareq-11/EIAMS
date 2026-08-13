namespace Application.Abstractions.Pagination;

public static class PaginationDefaults
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;
    public const int MaximumPage = int.MaxValue / MaximumPageSize;
}
