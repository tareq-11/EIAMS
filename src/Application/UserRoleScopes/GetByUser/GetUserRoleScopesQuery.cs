using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.UserRoleScopes.GetByUser;

public sealed record GetUserRoleScopesQuery(Guid UserId, int Page, int PageSize)
    : IQuery<PagedResult<UserRoleScopeResponse>>;
