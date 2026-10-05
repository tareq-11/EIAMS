using Domain.Common;
using Domain.Roles;
using FluentValidation;

namespace Application.Roles.Update;

internal sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(c => c.RoleId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleFor(c => c.NameAr).NotEmpty().MaximumLength(Role.NameArMaxLength);
        RuleFor(c => c.ExpectedRowVersion).GreaterThanOrEqualTo(1);
        RuleForEach(c => c.AllowedScopeTypes)
            .IsInEnum()
            .Must(UserAssignmentScopeTypes.IsAllowed)
            .WithMessage("OrganizationalUnit is not an allowed user assignment scope.");
    }
}