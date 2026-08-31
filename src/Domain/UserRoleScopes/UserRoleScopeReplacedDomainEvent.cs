using Domain.Common;
using SharedKernel;

namespace Domain.UserRoleScopes;

public sealed record UserRoleScopeReplacedDomainEvent(
    Guid UserRoleScopeId,
    Guid UserId,
    Guid PreviousRoleId,
    ScopeType PreviousScopeType,
    Guid? PreviousScopeId,
    Guid RoleId,
    ScopeType ScopeType,
    Guid? ScopeId) : IDomainEvent;
