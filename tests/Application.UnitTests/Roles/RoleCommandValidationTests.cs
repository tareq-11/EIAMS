using Application.Roles.Create;
using Application.Roles.Update;
using Domain.Roles;
using FluentValidation;
using FluentValidation.Results;

namespace Application.UnitTests.Roles;

public sealed class RoleCommandValidationTests
{
    /// <summary>A role that starts with no grants is legitimate; only an absent member is rejected.</summary>
    private static readonly string[] NoPermissions = [];

    [Fact]
    public void CreateRole_ShouldRejectBlankNameAr()
    {
        ValidationResult result = new CreateRoleCommandValidator()
            .Validate(new CreateRoleCommand("WH_KEEPER", "   ", null, NoPermissions));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(CreateRoleCommand.NameAr));
    }

    [Fact]
    public void CreateRole_ShouldRejectNameArLongerThanTheColumn()
    {
        ValidationResult result = new CreateRoleCommandValidator()
            .Validate(new CreateRoleCommand("WH_KEEPER", new string('د', Role.NameArMaxLength + 1), null, NoPermissions));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(CreateRoleCommand.NameAr));
    }

    [Fact]
    public void CreateRole_ShouldAcceptNameArAtTheColumnMaximum()
    {
        ValidationResult result = new CreateRoleCommandValidator()
            .Validate(new CreateRoleCommand("WH_KEEPER", new string('د', Role.NameArMaxLength), null, NoPermissions));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void UpdateRole_ShouldRejectBlankNameAr()
    {
        ValidationResult result = new UpdateRoleCommandValidator()
            .Validate(new UpdateRoleCommand(Guid.NewGuid(), "WH_KEEPER", string.Empty, null, 1));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(failure => failure.PropertyName == nameof(UpdateRoleCommand.NameAr));
    }

    [Fact]
    public void UpdateRole_ShouldAcceptArabicLabelIndependentlyOfTheRoleCode()
    {
        ValidationResult result = new UpdateRoleCommandValidator()
            .Validate(new UpdateRoleCommand(Guid.NewGuid(), "WH_KEEPER", "أمين المستودع", null, 1));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void UpdateRole_ShouldRejectNonPositiveExpectedRowVersion()
    {
        ValidationResult result = new UpdateRoleCommandValidator()
            .Validate(new UpdateRoleCommand(Guid.NewGuid(), "WH_KEEPER", "أمين المستودع", null, 0));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(
            failure => failure.PropertyName == nameof(UpdateRoleCommand.ExpectedRowVersion));
    }
}