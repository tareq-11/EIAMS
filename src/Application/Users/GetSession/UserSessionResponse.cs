namespace Application.Users.GetSession;

public sealed record UserSessionResponse(
    UserSessionUserDto User,
    UserSessionRoleDto Role,
    UserSessionScopeDto Scope,
    IReadOnlyList<string> PermissionCodes);

public sealed record UserSessionUserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    Guid? EmployeeId,
    string? EmployeeName);

public sealed record UserSessionRoleDto(
    Guid Id,
    string Name,
    string? Description);

public sealed record UserSessionScopeDto(
    string ScopeType,
    Guid? ScopeId,
    string ScopeName);
