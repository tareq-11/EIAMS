using System.Text.Json.Serialization;
using Domain.Common;

namespace Application.Users.GetSession;

/// <summary>
/// Authoritative singular session projection (D-SRS-01). JSON property names
/// are explicitly mapped via <see cref="JsonPropertyNameAttribute"/> so the wire
/// contract stays stable regardless of C# rename or PascalCase vs camelCase
/// serializer configuration.
/// </summary>
public sealed record UserSessionResponse(
    [property: JsonPropertyName("user")] UserSessionUserDto User,
    [property: JsonPropertyName("role")] UserSessionRoleDto Role,
    [property: JsonPropertyName("activeScope")] UserSessionScopeDto Scope,
    [property: JsonPropertyName("permissionCodes")] IReadOnlyList<string> PermissionCodes);

public sealed record UserSessionUserDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("firstName")] string FirstName,
    [property: JsonPropertyName("lastName")] string LastName,
    [property: JsonPropertyName("employeeId")] Guid? EmployeeId,
    [property: JsonPropertyName("employeeName")] string? EmployeeName);

public sealed record UserSessionRoleDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description);

public sealed record UserSessionScopeDto(
    [property: JsonPropertyName("scopeType")] UserAssignmentScopeType ScopeType,
    [property: JsonPropertyName("scopeId")] Guid? ScopeId,
    [property: JsonPropertyName("scopeName")] string ScopeName);
