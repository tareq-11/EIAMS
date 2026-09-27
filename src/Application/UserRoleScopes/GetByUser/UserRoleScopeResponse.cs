using Domain.Common;

namespace Application.UserRoleScopes.GetByUser;

public sealed class UserRoleScopeResponse
{
    public Guid Id { get; init; }

    public Guid RoleId { get; init; }

    public string RoleName { get; init; }

    public UserAssignmentScopeType ScopeType { get; init; }

    public Guid? ScopeId { get; init; }

    public int RowVersion { get; init; }
}
