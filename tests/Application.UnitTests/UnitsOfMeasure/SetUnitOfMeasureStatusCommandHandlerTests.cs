using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.UnitTests.Abstractions;
using Application.UnitsOfMeasure.SetStatus;
using Domain.Common;
using Domain.Materials;
using Domain.UnitsOfMeasure;
using NSubstitute;
using SharedKernel;

namespace Application.UnitTests.UnitsOfMeasure;

public sealed class SetUnitOfMeasureStatusCommandHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_Should_DeactivateUnusedUnit()
    {
        await using TestDbContext context = CreateDbContext();
        var unit = UnitOfMeasure.Create(Guid.NewGuid(), "Piece", "pc", "Count");
        context.UnitsOfMeasure.Add(unit);
        await context.SaveChangesAsync();
        SetUnitOfMeasureStatusCommandHandler handler = CreateHandler(context);

        Result result = await handler.Handle(
            new SetUnitOfMeasureStatusCommand(unit.Id, Status.Inactive), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        unit.Status.ShouldBe(Status.Inactive);
    }

    [Fact]
    public async Task Handle_Should_RejectDeactivationWhenUnitIsUsedByMaterial()
    {
        await using TestDbContext context = CreateDbContext();
        var unit = UnitOfMeasure.Create(Guid.NewGuid(), "Piece", "pc", "Count");
        var material = Material.Create(
            Guid.NewGuid(), Guid.NewGuid(), unit.Id, "مادة", "Material", "MAT-UOM-USED",
            MaterialKind.Consumable, TrackingType.Quantity, false, null);
        context.UnitsOfMeasure.Add(unit);
        context.Materials.Add(material);
        await context.SaveChangesAsync();
        SetUnitOfMeasureStatusCommandHandler handler = CreateHandler(context);

        Result result = await handler.Handle(
            new SetUnitOfMeasureStatusCommand(unit.Id, Status.Inactive), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(UnitOfMeasureErrors.InUse(unit.Id));
        unit.Status.ShouldBe(Status.Active);
    }

    private static SetUnitOfMeasureStatusCommandHandler CreateHandler(TestDbContext context)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        IScopeAuthorizationService authorization = Substitute.For<IScopeAuthorizationService>();
        authorization.HasPermissionInScopeAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<ScopeType>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(true);
        return new SetUnitOfMeasureStatusCommandHandler(context, userContext, authorization);
    }
}
