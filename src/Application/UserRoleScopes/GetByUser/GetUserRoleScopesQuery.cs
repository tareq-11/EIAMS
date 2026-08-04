using Application.Abstractions.Messaging;

namespace Application.UserRoleScopes.GetByUser;

public sealed record GetUserRoleScopesQuery(Guid UserId) : IQuery<List<UserRoleScopeResponse>>;
