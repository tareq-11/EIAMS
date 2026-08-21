using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.M7;

[Collection(nameof(IntegrationTestCollection))]
public sealed class M7InventoryCountApiTests : BaseIntegrationTest
{
    private static readonly Guid CountId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid WarehouseId = Guid.Parse("10000000-0000-0000-0000-000000000002");

    public M7InventoryCountApiTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("inventory-counts?warehouseId=10000000-0000-0000-0000-000000000002")]
    [InlineData("inventory-counts/10000000-0000-0000-0000-000000000001")]
    [InlineData("inventory-counts/10000000-0000-0000-0000-000000000001/lines")]
    [InlineData("warehouses/10000000-0000-0000-0000-000000000002/inventory-freeze-status")]
    public async Task CountReadRoutes_Should_ReturnUnauthorized_WhenTokenIsMissing(string route)
    {
        // Arrange

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync(route);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CountWriteRoutes_Should_ReturnUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        var lineId = Guid.NewGuid();

        // Act
        HttpResponseMessage plan = await HttpClient.PostAsJsonAsync("inventory-counts", new
        {
            warehouseId = WarehouseId,
            countType = "Scheduled",
            scopeType = "EntireWarehouse",
            materialIds = Array.Empty<Guid>(),
            freezePolicy = "NoFreeze"
        });
        HttpResponseMessage start = await HttpClient.PostAsJsonAsync(
            $"inventory-counts/{CountId}/start", new { expectedRowVersion = 1 });
        HttpResponseMessage actuals = await HttpClient.PutAsJsonAsync(
            $"inventory-counts/{CountId}/actuals",
            new { actuals = new[] { new { lineId, actualQuantity = 1m } }, expectedRowVersion = 2 });
        HttpResponseMessage complete = await HttpClient.PostAsJsonAsync(
            $"inventory-counts/{CountId}/complete", new { expectedRowVersion = 3 });
        HttpResponseMessage close = await HttpClient.PostAsJsonAsync(
            $"inventory-counts/{CountId}/close", new { expectedRowVersion = 4 });

        // Assert
        plan.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        start.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        actuals.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        complete.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        close.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
