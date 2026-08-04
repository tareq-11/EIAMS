using SharedKernel;

namespace Domain.UserRoleScopes;

public sealed record UserRoleScopeRevokedDomainEvent(Guid UserRoleScopeId, Guid UserId, Guid RoleId) : IDomainEvent;
