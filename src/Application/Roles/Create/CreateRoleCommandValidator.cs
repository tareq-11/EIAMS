using Domain.Common;
using Domain.Roles;
using FluentValidation;

namespace Application.Roles.Create;

internal sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    /// <summary>
    /// Matches the widest code in the active dotted vocabulary with room to spare, so a legitimate
    /// code is never rejected for length while an obviously bogus payload still is.
    /// </summary>
    private const int MaxPermissionCodeLength = 100;

    public CreateRoleCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.NameAr).NotEmpty().MaximumLength(Role.NameArMaxLength);

        // An empty set is legitimate: it creates a role that grants nothing until permissions are
        // added deliberately. Only an absent member is rejected.
        RuleFor(c => c.PermissionCodes).NotNull();
        RuleForEach(c => c.PermissionCodes)
            .NotEmpty()
            .MaximumLength(MaxPermissionCodeLength);

        RuleForEach(c => c.AllowedScopeTypes)
            .IsInEnum()
            .Must(UserAssignmentScopeTypes.IsAllowed)
            .WithMessage("OrganizationalUnit is not an allowed user assignment scope.");
    }
}