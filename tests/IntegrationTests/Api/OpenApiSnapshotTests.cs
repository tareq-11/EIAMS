using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Web.Api;

namespace IntegrationTests.Api;

[Collection(nameof(IntegrationTestCollection))]
public sealed class OpenApiSnapshotTests(IntegrationTestWebAppFactory factory)
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    [Fact]
    public async Task GeneratedSwagger_ShouldMatchCheckedInSnapshot()
    {
        using WebApplicationFactory<Program> developmentFactory = factory.WithWebHostBuilder(
            builder => builder.UseEnvironment("Development"));
        using HttpClient client = developmentFactory.CreateClient();
        string generated = await client.GetStringAsync("/swagger/v1/swagger.json");
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        string snapshotPath = Path.Combine(repositoryRoot, "contracts", "openapi", "eiams-backend-v1.openapi.json");

        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI_SNAPSHOT") == "1")
        {
            await File.WriteAllTextAsync(snapshotPath, JsonSerializer.Serialize(
                JsonDocument.Parse(generated).RootElement,
                IndentedJson) + Environment.NewLine);
            return;
        }

        using var actual = JsonDocument.Parse(generated);
        using var expected = JsonDocument.Parse(await File.ReadAllTextAsync(snapshotPath));
        JsonElement.DeepEquals(actual.RootElement, expected.RootElement).ShouldBeTrue();
    }
}
