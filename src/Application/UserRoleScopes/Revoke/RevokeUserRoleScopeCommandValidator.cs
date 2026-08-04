using FluentValidation;

namespace Application.UserRoleScopes.Revoke;

internal sealed class RevokeUserRoleScopeCommandValidator : AbstractValidator<RevokeUserRoleScopeCommand>
{
    public RevokeUserRoleScopeCommandValidator()
    {
        RuleFor(c => c.UserRoleScopeId).NotEmpty();
    }
}
