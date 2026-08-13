using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;

namespace Application.RolePermissions.GetByRole;

public sealed record GetRolePermissionsQuery(Guid RoleId, int Page, int PageSize)
    : IQuery<PagedResult<PermissionResponse>>;
