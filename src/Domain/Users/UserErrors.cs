using SharedKernel;

namespace Domain.Users;

public static class UserErrors
{
    public static Error NotFound(Guid userId) => Error.NotFound(
        "Users.NotFound",
        $"The user with the Id = '{userId}' was not found");

    public static Error Unauthorized() => Error.Forbidden(
        "Users.Unauthorized",
        "You are not authorized to perform this action.");

    public static readonly Error NotFoundByEmail = Error.NotFound(
        "Users.NotFoundByEmail",
        "The user with the specified email was not found");

    public static readonly Error EmailNotUnique = Error.Conflict(
        "Users.EmailNotUnique",
        "The provided email is not unique");

    public static readonly Error RegistrationClosed = Error.Forbidden(
        "Users.RegistrationClosed",
        "Public registration is closed. User accounts must be created by an administrator.");

    public static readonly Error AdministratorRecoveryUnavailable = Error.Forbidden(
        "Users.AdministratorRecoveryUnavailable",
        "Administrator recovery is unavailable.");

    public static readonly Error EmployeeAlreadyLinked = Error.Conflict(
        "Users.EmployeeAlreadyLinked",
        "The employee is already linked to another user account");

    public static readonly Error Suspended = Error.Forbidden(
        "Users.Suspended",
        "The user account is suspended.");

    public static readonly Error AdministrationRequiresEnterpriseScope = Error.Forbidden(
        "Users.AdministrationRequiresEnterpriseScope",
        "User administration requires Enterprise scope.");

    public static readonly Error SelfSuspensionNotAllowed = Error.Conflict(
        "Users.SelfSuspensionNotAllowed",
        "You cannot suspend your own account.");

    public static readonly Error InvalidRefreshToken = Error.Problem(
        "Users.InvalidRefreshToken",
        "The provided refresh token is invalid or has expired");
}
