using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.Roles.GetList;

public sealed record GetRolesQuery(int Page, int PageSize) : IQuery<PagedResult<RoleResponse>>;
