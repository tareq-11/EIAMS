using Application.Abstractions.Messaging;

namespace Application.RolePermissions.GetByRole;

public sealed record GetRolePermissionsQuery(Guid RoleId) : IQuery<List<PermissionResponse>>;
