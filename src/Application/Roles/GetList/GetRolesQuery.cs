using Application.Abstractions.Messaging;

namespace Application.Roles.GetList;

public sealed record GetRolesQuery : IQuery<List<RoleResponse>>;
