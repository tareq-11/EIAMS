using Application.Abstractions.Messaging;
using Application.UserRoleScopes.GetByUser;

namespace Application.UserRoleScopes.GetAssignment;

public sealed record GetUserRoleScopeQuery(Guid UserId) : IQuery<UserRoleScopeResponse>;
