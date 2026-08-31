namespace Application.Users.GetList;

public sealed record UserAdministrationResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    Guid? EmployeeId,
    string? EmployeeName,
    string Status,
    DateTime? LastLoginUtc,
    DateTime CreatedAtUtc,
    Guid? RoleId,
    string? RoleName,
    string? ScopeType,
    Guid? ScopeId);
