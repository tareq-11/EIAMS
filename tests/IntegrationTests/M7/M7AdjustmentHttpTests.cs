using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.M7;

public sealed class M7AdjustmentHttpTests(IntegrationTestWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task CreateDisposal_Should_ReturnUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        var request = new
        {
            warehouseId = Guid.NewGuid(),
            assetIds = new[] { Guid.NewGuid() },
            reason = "Damaged"
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            "inventory-adjustments/disposals", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddAdjustmentLine_Should_ReturnUnauthorized_WhenTokenIsMissing()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var request = new
        {
            materialId = Guid.NewGuid(),
            difference = -1m,
            unitId = (Guid?)null,
            reason = "Variance",
            expectedRowVersion = 1
        };

        // Act
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(
            $"inventory-adjustments/{documentId}/lines", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
