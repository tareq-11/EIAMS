using System.ComponentModel.DataAnnotations;
using Application.Abstractions.Pagination;

namespace Web.Api.Infrastructure;

public sealed class PaginationQueryParameters
{
    [Range(1, PaginationDefaults.MaximumPage)]
    public int Page { get; set; } = PaginationDefaults.DefaultPage;

    [Range(1, PaginationDefaults.MaximumPageSize)]
    public int PageSize { get; set; } = PaginationDefaults.DefaultPageSize;
}
