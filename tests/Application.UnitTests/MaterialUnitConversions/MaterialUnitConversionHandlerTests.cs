using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.MaterialUnitConversions.Add;
using Application.MaterialUnitConversions.GetById;
using Application.MaterialUnitConversions.GetByMaterial;
using Application.MaterialUnitConversions.Remove;
using Application.MaterialUnitConversions.Update;
using Application.UnitTests.Abstractions;
using Domain.Common;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.MaterialUnitConversions;
using Domain.UnitsOfMeasure;
using NSubstitute;
using Shouldly;
using SharedKernel;

namespace Application.UnitTests.MaterialUnitConversions;

public sealed class MaterialUnitConversionHandlerTests : BaseHandlerTest
{
    private static async Task<(Guid MaterialId, Guid BaseUnitId, Guid SourceUnitId)> SeedMaterialAsync(TestDbContext context)
    {
        var domainId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var baseUnitId = Guid.NewGuid();
        var sourceUnitId = Guid.NewGuid();
        var materialId = Guid.NewGuid();

        context.MaterialDomains.Add(MaterialDomain.Create(domainId, "Domain", "DOM"));
        context.MaterialCategories.Add(MaterialCategory.Create(categoryId, domainId, null, "Category", "CAT"));
        context.UnitsOfMeasure.Add(UnitOfMeasure.Create(baseUnitId, "Piece", "pc", "Count"));
        context.UnitsOfMeasure.Add(UnitOfMeasure.Create(sourceUnitId, "Box", "box", "Count"));
        context.MaterialFamilies.Add(MaterialFamily.Create(familyId, categoryId, "Family", "FAM", baseUnitId));
        context.Materials.Add(Material.Create(
            materialId,
            familyId,
            baseUnitId,
            "مادة",
            "Material",
            $"MAT-{materialId:N}",
            MaterialKind.Consumable,
            TrackingType.Quantity,
            false,
            null));

        await context.SaveChangesAsync();

        return (materialId, baseUnitId, sourceUnitId);
    }

    private static IUserContext CreateUserContext()
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        return userContext;
    }

    private static IScopeAuthorizationService CreateAuthorization(bool authorized)
    {
        IScopeAuthorizationService authorization = Substitute.For<IScopeAuthorizationService>();
        authorization.HasPermissionInScopeAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<ScopeType>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(authorized));
        return authorization;
    }

    [Fact]
    public async Task AddMaterialUnitConversion_Should_Succeed_WhenValid()
    {
        await using TestDbContext context = CreateDbContext();
        (Guid materialId, Guid baseUnitId, Guid sourceUnitId) = await SeedMaterialAsync(context);

        var handler = new AddMaterialUnitConversionCommandHandler(
            context,
            CreateUserContext(),
            CreateAuthorization(true));

        var command = new AddMaterialUnitConversionCommand(materialId, sourceUnitId, baseUnitId, 10m);

        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task GetMaterialUnitConversionById_Should_ReturnConversion_WhenExists()
    {
        await using TestDbContext context = CreateDbContext();
        (Guid materialId, Guid baseUnitId, Guid sourceUnitId) = await SeedMaterialAsync(context);

        var conversion = MaterialUnitConversion.Create(
            Guid.NewGuid(),
            materialId,
            sourceUnitId,
            baseUnitId,
            12m);
        context.MaterialUnitConversions.Add(conversion);
        await context.SaveChangesAsync();

        var handler = new GetMaterialUnitConversionByIdQueryHandler(context);
        var query = new GetMaterialUnitConversionByIdQuery(materialId, conversion.Id);

        Result<MaterialUnitConversionResponse> result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(conversion.Id);
        result.Value.Factor.ShouldBe(12m);
        result.Value.FromUnitId.ShouldBe(sourceUnitId);
        result.Value.ToBaseUnitId.ShouldBe(baseUnitId);
    }

    [Fact]
    public async Task UpdateMaterialUnitConversion_Should_UpdateFactor_WhenValid()
    {
        await using TestDbContext context = CreateDbContext();
        (Guid materialId, Guid baseUnitId, Guid sourceUnitId) = await SeedMaterialAsync(context);

        var conversion = MaterialUnitConversion.Create(
            Guid.NewGuid(),
            materialId,
            sourceUnitId,
            baseUnitId,
            12m);
        context.MaterialUnitConversions.Add(conversion);
        await context.SaveChangesAsync();

        var handler = new UpdateMaterialUnitConversionCommandHandler(
            context,
            CreateUserContext(),
            CreateAuthorization(true));

        var command = new UpdateMaterialUnitConversionCommand(materialId, conversion.Id, 24m);

        Result result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        conversion.Factor.ShouldBe(24m);
    }

    [Fact]
    public async Task RemoveMaterialUnitConversion_Should_Succeed_WhenAuthorized()
    {
        await using TestDbContext context = CreateDbContext();
        (Guid materialId, Guid baseUnitId, Guid sourceUnitId) = await SeedMaterialAsync(context);

        var conversion = MaterialUnitConversion.Create(
            Guid.NewGuid(),
            materialId,
            sourceUnitId,
            baseUnitId,
            12m);
        context.MaterialUnitConversions.Add(conversion);
        await context.SaveChangesAsync();

        var handler = new RemoveMaterialUnitConversionCommandHandler(
            context,
            CreateUserContext(),
            CreateAuthorization(true));

        var command = new RemoveMaterialUnitConversionCommand(conversion.Id);

        Result result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }
}
