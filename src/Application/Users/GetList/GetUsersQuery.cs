using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Users;

namespace Application.Users.GetList;

public sealed record GetUsersQuery(
    string? Search = null,
    UserStatus? Status = null,
    bool? HasRoleScope = null,
    int Page = PaginationDefaults.DefaultPage,
    int PageSize = PaginationDefaults.DefaultPageSize)
    : IQuery<PagedResult<UserAdministrationResponse>>;
