using Application.Abstractions.Messaging;
using Application.UserRoleScopes.GetByUser;
using Domain.Common;

namespace Application.UserRoleScopes.Replace;

public sealed record ReplaceUserRoleScopeCommand(
    Guid UserId,
    Guid RoleId,
    ScopeType ScopeType,
    Guid? ScopeId,
    int ExpectedRowVersion) : ICommand<UserRoleScopeResponse>;
