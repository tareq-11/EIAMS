using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.Permissions.GetList;

public sealed record GetPermissionsQuery(int Page, int PageSize) : IQuery<PagedResult<PermissionResponse>>;
