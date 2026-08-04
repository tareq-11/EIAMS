using FluentValidation;

namespace Application.RolePermissions.Remove;

internal sealed class RemovePermissionFromRoleCommandValidator : AbstractValidator<RemovePermissionFromRoleCommand>
{
    public RemovePermissionFromRoleCommandValidator()
    {
        RuleFor(c => c.RoleId).NotEmpty();
        RuleFor(c => c.PermissionId).NotEmpty();
    }
}
