using Domain.Common;
using FluentValidation;

namespace Application.UserRoleScopes.Grant;

internal sealed class GrantUserRoleScopeCommandValidator : AbstractValidator<GrantUserRoleScopeCommand>
{
    public GrantUserRoleScopeCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.RoleId).NotEmpty();
        RuleFor(c => c.ScopeType).IsInEnum();

        RuleFor(c => c.ScopeId)
            .Null()
            .When(c => c.ScopeType == ScopeType.Enterprise)
            .WithMessage("A scope id must not be provided for Enterprise scoped grants.");

        RuleFor(c => c.ScopeId)
            .NotNull()
            .When(c => c.ScopeType != ScopeType.Enterprise)
            .WithMessage("A scope id is required for Site and Warehouse scoped grants.");
    }
}
