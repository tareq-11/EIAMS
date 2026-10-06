using System.Text.Json.Serialization;
using Domain.Common;

namespace Application.UserRoleScopes.GetByUser;

public sealed class UserRoleScopeResponse
{
    public Guid Id { get; init; }

    public Guid RoleId { get; init; }

    public string RoleName { get; init; }

    // This response is emitted through Results.Ok, which serializes with the minimal-API
    // JsonOptions rather than the MVC options the global JsonStringEnumConverter is
    // registered on, so without an explicit converter the enum is written as its numeric
    // ordinal (0) while the OpenAPI document declares
    // {"type":"string","enum":["Enterprise","Site","Warehouse"]}. The session DTO
    // (UserSessionScopeDto) already carries this attribute for the same reason.
    [JsonConverter(typeof(JsonStringEnumConverter<UserAssignmentScopeType>))]
    public UserAssignmentScopeType ScopeType { get; init; }

    public Guid? ScopeId { get; init; }

    public int RowVersion { get; init; }
}
