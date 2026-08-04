using Domain.Common;
using SharedKernel;

namespace Domain.UserRoleScopes;

public sealed record UserRoleScopeGrantedDomainEvent(
    Guid UserRoleScopeId,
    Guid UserId,
    Guid RoleId,
    ScopeType ScopeType,
    Guid? ScopeId) : IDomainEvent;
