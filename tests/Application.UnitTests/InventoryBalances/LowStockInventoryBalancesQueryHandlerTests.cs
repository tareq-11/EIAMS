using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.InventoryBalances.GetLowStock;
using Application.Abstractions.Pagination;
using Application.UnitTests.Abstractions;
using Domain.Common;
using Domain.InventoryBalances;
using Domain.Materials;
using Domain.WarehouseMaterialSettings;
using Domain.Warehouses;
using NSubstitute;
using SharedKernel;

namespace Application.UnitTests.InventoryBalances;

public sealed class LowStockInventoryBalancesQueryHandlerTests : BaseHandlerTest
{
    [Fact]
    public async Task Handle_ShouldIncludeActiveSettingsWithNoBalanceAsZeroAndExcludeNonLowStock()
    {
        await using TestDbContext context = CreateDbContext();
        (Warehouse warehouse, Material material) = AddCatalogPair(context, "A");

        WarehouseMaterialSetting noBalance = AddSetting(context, warehouse, material, min: 3m, max: 10m);
        WarehouseMaterialSetting inactive = AddSetting(context, warehouse, CreateMaterial("INACTIVE"), min: 3m, max: 10m);
        inactive.SetStatus(Status.Inactive);
        WarehouseMaterialSetting atThreshold = AddSetting(context, warehouse, CreateMaterial("THRESHOLD"), min: 2m, max: 10m);
        AddBalance(context, warehouse, atThreshold.MaterialId, 2m);
        WarehouseMaterialSetting lowBalance = AddSetting(context, warehouse, CreateMaterial("LOW"), min: 5m, max: 10m);
        AddBalance(context, warehouse, lowBalance.MaterialId, 1m);
        await context.SaveChangesAsync();
        GetLowStockInventoryBalancesQueryHandler handler = CreateHandler(
            context, new WarehousePermissionScope(true, new HashSet<Guid>()));

        Result<PagedResult<LowStockInventoryBalanceResponse>> result = await handler.Handle(
            new GetLowStockInventoryBalancesQuery(null, 1, 20), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalItems.ShouldBe(2);
        result.Value.Items.Select(item => item.SettingId).ShouldContain(noBalance.Id);
        result.Value.Items.Single(item => item.SettingId == noBalance.Id).Quantity.ShouldBe(0m);
        result.Value.Items.Select(item => item.SettingId).ShouldContain(lowBalance.Id);
        result.Value.Items.ShouldAllBe(item => item.Quantity < item.MinQuantity);
    }

    [Fact]
    public async Task Handle_ShouldRestrictToAuthorizedWarehouseAndUseOneBasedStablePagination()
    {
        await using TestDbContext context = CreateDbContext();
        (Warehouse allowedA, Material materialA) = AddCatalogPair(context, "A");
        (Warehouse allowedB, Material materialB) = AddCatalogPair(context, "B");
        (Warehouse denied, Material materialDenied) = AddCatalogPair(context, "C");
        WarehouseMaterialSetting settingA = AddSetting(context, allowedA, materialA, 1m, 5m);
        WarehouseMaterialSetting settingB = AddSetting(context, allowedB, materialB, 1m, 5m);
        WarehouseMaterialSetting deniedSetting = AddSetting(context, denied, materialDenied, 1m, 5m);
        await context.SaveChangesAsync();
        GetLowStockInventoryBalancesQueryHandler handler = CreateHandler(
            context,
            new WarehousePermissionScope(false, new HashSet<Guid> { allowedA.Id, allowedB.Id }));

        Result<PagedResult<LowStockInventoryBalanceResponse>> result = await handler.Handle(
            new GetLowStockInventoryBalancesQuery(null, 2, 1), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.TotalItems.ShouldBe(2);
        result.Value.Page.ShouldBe(2);
        result.Value.Items.Count.ShouldBe(1);
        result.Value.Items[0].SettingId.ShouldBe(settingB.Id);
        result.Value.Items.Select(item => item.SettingId).ShouldNotContain(deniedSetting.Id);
        settingA.Id.ShouldNotBe(settingB.Id);
    }

    [Fact]
    public async Task Handle_ShouldFailWhenUserHasNoWarehouseScope()
    {
        await using TestDbContext context = CreateDbContext();
        GetLowStockInventoryBalancesQueryHandler handler = CreateHandler(
            context, new WarehousePermissionScope(false, new HashSet<Guid>()));

        Result<PagedResult<LowStockInventoryBalanceResponse>> result = await handler.Handle(
            new GetLowStockInventoryBalancesQuery(null, 1, 20), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(InventoryBalanceErrors.Forbidden);
    }

    private static (Warehouse Warehouse, Material Material) AddCatalogPair(TestDbContext context, string suffix)
    {
        var warehouse = Warehouse.Create(
            Guid.NewGuid(), Guid.NewGuid(), $"Warehouse {suffix}", $"WH-{suffix}", "Main", true);
        Material material = CreateMaterial($"MAT-{suffix}");
        context.Warehouses.Add(warehouse);
        context.Materials.Add(material);
        return (warehouse, material);
    }

    private static Material CreateMaterial(string code) => Material.Create(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "مادة", "Material", code,
        MaterialKind.Consumable, TrackingType.Quantity, false, null);

    private static WarehouseMaterialSetting AddSetting(
        TestDbContext context,
        Warehouse warehouse,
        Material material,
        decimal min,
        decimal max)
    {
        WarehouseMaterialSetting setting = WarehouseMaterialSetting.Create(
            Guid.NewGuid(), warehouse.Id, material.Id, min, max).Value;
        context.WarehouseMaterialSettings.Add(setting);
        context.Materials.Add(material);
        return setting;
    }

    private static void AddBalance(TestDbContext context, Warehouse warehouse, Guid materialId, decimal quantity)
    {
        var balance = InventoryBalance.CreateZero(Guid.NewGuid(), warehouse.Id, materialId, DateTime.UtcNow);
        balance.SetQuantity(quantity, DateTime.UtcNow).IsSuccess.ShouldBeTrue();
        context.InventoryBalances.Add(balance);
    }

    private static GetLowStockInventoryBalancesQueryHandler CreateHandler(
        TestDbContext context,
        WarehousePermissionScope scope)
    {
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(Guid.NewGuid());
        IScopeAuthorizationService authorization = Substitute.For<IScopeAuthorizationService>();
        authorization.GetWarehousePermissionScopeAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(scope);
        return new GetLowStockInventoryBalancesQueryHandler(context, userContext, authorization);
    }
}
