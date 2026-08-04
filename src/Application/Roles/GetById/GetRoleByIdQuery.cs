using Application.Abstractions.Messaging;

namespace Application.Roles.GetById;

public sealed record GetRoleByIdQuery(Guid RoleId) : IQuery<RoleResponse>;
