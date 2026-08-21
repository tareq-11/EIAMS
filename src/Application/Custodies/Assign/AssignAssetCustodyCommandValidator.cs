using FluentValidation;

namespace Application.Custodies.Assign;

internal sealed class AssignAssetCustodyCommandValidator : AbstractValidator<AssignAssetCustodyCommand>
{
    public AssignAssetCustodyCommandValidator()
    {
        RuleFor(command => command.AssetId).NotEmpty();
        RuleFor(command => command.EmployeeId).NotEmpty();
        RuleFor(command => command.ExpectedCustodyRowVersion).GreaterThan(0);
        RuleFor(command => command.Note).MaximumLength(300);
    }
}
