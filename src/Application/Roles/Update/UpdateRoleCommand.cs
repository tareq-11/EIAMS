using Application.Abstractions.Messaging;
using Application.Roles;
using Domain.Common;

namespace Application.Roles.Update;

/// <param name="ExpectedRowVersion">
/// The <c>rowVersion</c> the caller last read. A mismatch is rejected as
/// <c>Roles.RowVersionMismatch</c> (409) instead of overwriting a concurrent change.
/// </param>
public sealed record UpdateRoleCommand(
    Guid RoleId,
    string Name,
    string NameAr,
    string? Description,
    int ExpectedRowVersion,
    IReadOnlyCollection<ScopeType>? AllowedScopeTypes = null) : ICommand<RoleResponse>;