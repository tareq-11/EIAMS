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
    [property: JsonPropertyName("nameAr")] string NameAr,
    [property: JsonPropertyName("description")] string? Description);

public sealed record UserSessionScopeDto(
    // This response is emitted through Results.Ok, which serializes with the minimal-API
    // JsonOptions rather than the MVC options the global JsonStringEnumConverter is
    // registered on. Without an explicit converter the enum was written as its numeric
    // value (0) while the OpenAPI document declares {"type":"string","enum":["Enterprise",
    // "Site","Warehouse"]}, so generated clients expected a string and received a number.
    [property: JsonConverter(typeof(JsonStringEnumConverter<UserAssignmentScopeType>))]
    [property: JsonPropertyName("scopeType")] UserAssignmentScopeType ScopeType,
    [property: JsonPropertyName("scopeId")] Guid? ScopeId,
    [property: JsonPropertyName("scopeName")] string ScopeName);
