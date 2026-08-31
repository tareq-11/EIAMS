using Domain.Common;
using FluentValidation;

namespace Application.UserRoleScopes.Replace;

internal sealed class ReplaceUserRoleScopeCommandValidator : AbstractValidator<ReplaceUserRoleScopeCommand>
{
    public ReplaceUserRoleScopeCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.RoleId).NotEmpty();
        RuleFor(command => command.ScopeType).IsInEnum();

        RuleFor(command => command.ScopeId)
            .Null()
            .When(command => command.ScopeType == ScopeType.Enterprise);

        RuleFor(command => command.ScopeId)
            .NotNull()
            .When(command => command.ScopeType != ScopeType.Enterprise);
    }
}
