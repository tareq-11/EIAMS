using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.MaterialCategories.Create;
using Application.MaterialCategories.Move;
using Application.MaterialFamilies.Create;
using Application.Materials.Create;
using Application.Materials.GetById;
using Application.MaterialUnitConversions.Add;
using Application.UnitTests.Abstractions;
using Domain.Common;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.MaterialUnitConversions;
using Domain.UnitsOfMeasure;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.M1;

public sealed class MaterialCatalogHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task CreateMaterialCategory_Should_RejectParentFromAnotherDomain()
    {
        await using TestDbContext context = CreateDbContext();
        var requestedDomainId = Guid.NewGuid();
        var otherDomainId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        context.MaterialDomains.Add(MaterialDomain.Create(requestedDomainId, "Requested", "REQ"));
        context.MaterialDomains.Add(MaterialDomain.Create(otherDomainId, "Other", "OTH"));
        context.MaterialCategories.Add(MaterialCategory.Create(parentId, otherDomainId, null, "Parent", "PARENT"));
        await context.SaveChangesAsync();

        var handler = new CreateMaterialCategoryCommandHandler(context, CreateUserContext(), CreateAuthorization(true));
        var command = new CreateMaterialCategoryCommand(requestedDomainId, parentId, "Child", "CHILD");

        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MaterialCategoryErrors.ParentInDifferentDomain(parentId));
        (await context.MaterialCategories.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task MoveMaterialCategory_Should_RejectCircularTreeAndNotMutateCategory()
    {
        await using TestDbContext context = CreateDbContext();
        var domainId = Guid.NewGuid();
        var rootId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        context.MaterialDomains.Add(MaterialDomain.Create(domainId, "Domain", "DOM"));
        context.MaterialCategories.Add(MaterialCategory.Create(rootId, domainId, null, "Root", "ROOT"));
        context.MaterialCategories.Add(MaterialCategory.Create(childId, domainId, rootId, "Child", "CHILD"));
        await context.SaveChangesAsync();

        var handler = new MoveMaterialCategoryCommandHandler(context, CreateUserContext(), CreateAuthorization(true));

        Result result = await handler.Handle(new MoveMaterialCategoryCommand(rootId, childId), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MaterialCategoryErrors.CircularParent);
        (await context.MaterialCategories.SingleAsync(category => category.Id == rootId)).ParentCategoryId.ShouldBeNull();
    }

    [Fact]
    public void MoveMaterialCategoryValidator_Should_RejectSelfAsParent()
    {
        var categoryId = Guid.NewGuid();

        FluentValidation.Results.ValidationResult validation = new MoveMaterialCategoryCommandValidator()
            .Validate(new MoveMaterialCategoryCommand(categoryId, categoryId));

        validation.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateMaterialFamily_Should_RejectMissingBaseUnit()
    {
        await using TestDbContext context = CreateDbContext();
        var domainId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var missingUnitId = Guid.NewGuid();
        context.MaterialDomains.Add(MaterialDomain.Create(domainId, "Domain", "DOM"));
        context.MaterialCategories.Add(MaterialCategory.Create(categoryId, domainId, null, "Category", "CAT"));
        await context.SaveChangesAsync();

        var handler = new CreateMaterialFamilyCommandHandler(context, CreateUserContext(), CreateAuthorization(true));
        var command = new CreateMaterialFamilyCommand(categoryId, "Family", "FAM", missingUnitId);

        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MaterialFamilyErrors.BaseUnitNotFound(missingUnitId));
    }

    [Fact]
    public async Task CreateMaterial_Should_RejectDuplicateCode()
    {
        await using TestDbContext context = CreateDbContext();
        var familyId = Guid.NewGuid();
        context.MaterialFamilies.Add(MaterialFamily.Create(familyId, Guid.NewGuid(), "Family", "FAM", Guid.NewGuid()));
        context.Materials.Add(Material.Create(
            Guid.NewGuid(),
            familyId,
            "مادة",
            "Material",
            "MAT-1",
            MaterialKind.Consumable,
            TrackingType.Quantity,
            false,
            false,
            null));
        await context.SaveChangesAsync();

        var handler = new CreateMaterialCommandHandler(context, CreateUserContext(), CreateAuthorization(true));
        var command = new CreateMaterialCommand(
            familyId,
            "مادة أخرى",
            "Other material",
            "MAT-1",
            MaterialKind.Consumable,
            TrackingType.Quantity,
            false,
            false,
            null);

        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MaterialErrors.CodeNotUnique);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    public void CreateMaterialValidator_Should_RejectAttributesThatAreNotJsonObjects(string attributes)
    {
        var command = new CreateMaterialCommand(
            Guid.NewGuid(),
            "مادة",
            null,
            "MAT-1",
            MaterialKind.Consumable,
            TrackingType.Quantity,
            false,
            false,
            attributes);

        FluentValidation.Results.ValidationResult validation = new CreateMaterialCommandValidator().Validate(command);

        validation.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task AddMaterialUnitConversion_Should_RejectUnitThatIsNotFamilyBaseUnit()
    {
        await using TestDbContext context = CreateDbContext();
        CatalogSeed seed = await SeedCatalogAsync(context);
        var wrongTargetUnitId = Guid.NewGuid();
        context.UnitsOfMeasure.Add(UnitOfMeasure.Create(wrongTargetUnitId, "Box", "box", "Count"));
        await context.SaveChangesAsync();

        var handler = new AddMaterialUnitConversionCommandHandler(context, CreateUserContext(), CreateAuthorization(true));
        var command = new AddMaterialUnitConversionCommand(seed.MaterialId, seed.SourceUnitId, wrongTargetUnitId, 10m);

        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MaterialUnitConversionErrors.BaseUnitMismatch);
    }

    [Fact]
    public async Task AddMaterialUnitConversion_Should_RejectUnitsWithDifferentTypes()
    {
        await using TestDbContext context = CreateDbContext();
        CatalogSeed seed = await SeedCatalogAsync(context);
        var weightUnitId = Guid.NewGuid();
        context.UnitsOfMeasure.Add(UnitOfMeasure.Create(weightUnitId, "Kilogram", "kg", "Weight"));
        await context.SaveChangesAsync();

        var handler = new AddMaterialUnitConversionCommandHandler(context, CreateUserContext(), CreateAuthorization(true));
        var command = new AddMaterialUnitConversionCommand(seed.MaterialId, weightUnitId, seed.BaseUnitId, 10m);

        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(MaterialUnitConversionErrors.UnitTypeMismatch);
    }

    [Fact]
    public async Task AddMaterialUnitConversion_Should_CreateOneConversionAndRejectDuplicateSourceUnit()
    {
        await using TestDbContext context = CreateDbContext();
        CatalogSeed seed = await SeedCatalogAsync(context);
        var handler = new AddMaterialUnitConversionCommandHandler(context, CreateUserContext(), CreateAuthorization(true));
        var command = new AddMaterialUnitConversionCommand(seed.MaterialId, seed.SourceUnitId, seed.BaseUnitId, 12m);

        Result<Guid> created = await handler.Handle(command, CancellationToken.None);
        Result<Guid> duplicate = await handler.Handle(command, CancellationToken.None);

        created.IsSuccess.ShouldBeTrue();
        duplicate.IsFailure.ShouldBeTrue();
        duplicate.Error.ShouldBe(MaterialUnitConversionErrors.AlreadyExists);
        (await context.MaterialUnitConversions.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task GetMaterialById_Should_ExposeDomainAndBaseUnitThroughTheCatalogHierarchy()
    {
        await using TestDbContext context = CreateDbContext();
        CatalogSeed seed = await SeedCatalogAsync(context);
        var handler = new GetMaterialByIdQueryHandler(context);

        Result<MaterialResponse> result = await handler.Handle(new GetMaterialByIdQuery(seed.MaterialId), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.MaterialDomainId.ShouldBe(seed.DomainId);
        result.Value.BaseUnitId.ShouldBe(seed.BaseUnitId);
        result.Value.BaseUnitSymbol.ShouldBe("pc");
    }

    [Fact]
    public void AddMaterialUnitConversionValidator_Should_RejectNonPositiveFactor()
    {
        FluentValidation.Results.ValidationResult validation = new AddMaterialUnitConversionCommandValidator().Validate(
            new AddMaterialUnitConversionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0m));

        validation.IsValid.ShouldBeFalse();
    }

    private static async Task<CatalogSeed> SeedCatalogAsync(TestDbContext context)
    {
        var domainId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var baseUnitId = Guid.NewGuid();
        var sourceUnitId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var materialId = Guid.NewGuid();
        context.MaterialDomains.Add(MaterialDomain.Create(domainId, "Domain", "DOM"));
        context.MaterialCategories.Add(MaterialCategory.Create(categoryId, domainId, null, "Category", "CAT"));
        context.UnitsOfMeasure.Add(UnitOfMeasure.Create(baseUnitId, "Piece", "pc", "Count"));
        context.UnitsOfMeasure.Add(UnitOfMeasure.Create(sourceUnitId, "Box", "box", "Count"));
        context.MaterialFamilies.Add(MaterialFamily.Create(familyId, categoryId, "Family", "FAM", baseUnitId));
        context.Materials.Add(Material.Create(
            materialId,
            familyId,
            "مادة",
            "Material",
            $"MAT-{materialId:N}",
            MaterialKind.Consumable,
            TrackingType.Quantity,
            false,
            false,
            null));
        await context.SaveChangesAsync();

        return new CatalogSeed(domainId, baseUnitId, sourceUnitId, materialId);
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

    private sealed record CatalogSeed(Guid DomainId, Guid BaseUnitId, Guid SourceUnitId, Guid MaterialId);
}
