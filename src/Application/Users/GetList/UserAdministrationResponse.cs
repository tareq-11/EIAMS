namespace Application.Users.GetList;

/// <summary>
/// One row of the administration directory.
/// <para>
/// <c>Username</c> is served on reads because the administration UI must be able to
/// SHOW the current login. It used to be write-only: required by both the create and
/// the update request bodies but returned by neither read projection, so an
/// administrator could not see the value the API demanded they send, and any save
/// either failed validation or silently renamed the account. A field the API demands
/// but never discloses is not a usable contract.
/// </para>
/// </summary>
public sealed record UserAdministrationResponse(
    Guid Id,
    string Email,
    string Username,
    string FirstName,
    string LastName,
    Guid? EmployeeId,
    string? EmployeeName,
    string Status,
    DateTime? LastLoginUtc,
    DateTime CreatedAtUtc,
    /// <summary>
    /// Concurrency token to submit as <c>expectedRowVersion</c> on the next write.
    /// Advances on every profile or status change.
    /// </summary>
    int RowVersion,
    Guid? RoleId,
    string? RoleName,
    string? ScopeType,
    Guid? ScopeId);
