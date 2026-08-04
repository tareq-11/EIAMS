using Application.Abstractions.Messaging;

namespace Application.Permissions.GetList;

public sealed record GetPermissionsQuery : IQuery<List<PermissionResponse>>;
