using FluentValidation;

namespace Application.RolePermissions.Assign;

internal sealed class AssignPermissionToRoleCommandValidator : AbstractValidator<AssignPermissionToRoleCommand>
{
    public AssignPermissionToRoleCommandValidator()
    {
        RuleFor(c => c.RoleId).NotEmpty();
        RuleFor(c => c.PermissionId).NotEmpty();
    }
}
