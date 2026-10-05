using Domain.Common;
using Domain.Roles;
using FluentValidation;

namespace Application.RolePermissions.Replace;

internal sealed class ReplaceRolePermissionsCommandValidator : AbstractValidator<ReplaceRolePermissionsCommand>
{
    /// <summary>
    /// Matches the widest code in the active dotted vocabulary with room to spare, so a legitimate
    /// code is never rejected for length while an obviously bogus payload still is.
    /// </summary>
    private const int MaxPermissionCodeLength = 100;

    public ReplaceRolePermissionsCommandValidator()
    {
        RuleFor(c => c.RoleId).NotEmpty();
        RuleFor(c => c.ExpectedRowVersion).GreaterThanOrEqualTo(1);

        // An empty set is legitimate: it removes every grant. Only null (an absent member) and
        // blank entries are rejected, and RolePermissionSetValidator decides what is grantable.
        RuleFor(c => c.PermissionCodes).NotNull();
        RuleForEach(c => c.PermissionCodes)
            .NotEmpty()
            .MaximumLength(MaxPermissionCodeLength);
    }
}