using Domain.Common;
using FluentValidation;

namespace Application.Roles.Create;

internal sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(100);
        RuleForEach(c => c.AllowedScopeTypes)
            .IsInEnum()
            .Must(UserAssignmentScopeTypes.IsAllowed)
            .WithMessage("OrganizationalUnit is not an allowed user assignment scope.");
    }
}
