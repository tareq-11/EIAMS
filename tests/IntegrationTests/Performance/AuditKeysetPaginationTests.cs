using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Performance;

[Collection(nameof(IntegrationTestCollection))]
public sealed class AuditKeysetPaginationTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public AuditKeysetPaginationTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task CursorEndpoint_ShouldReturnStableNonOverlappingPagesWithoutCountQuery()
    {
        await AuthenticateAsAdministratorAsync();

        SqlCommandCounterInterceptor commandCounter = factory.Services
            .GetRequiredService<SqlCommandCounterInterceptor>();
        commandCounter.Reset();

        HttpResponseMessage firstResponse = await HttpClient.GetAsync("audit-logs/cursor?pageSize=1");
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var first = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        JsonElement firstItem = first.RootElement.GetProperty("data").GetProperty("items")[0];
        Guid firstId = firstItem.GetProperty("id").GetGuid();
        JsonElement pageInfo = first.RootElement.GetProperty("data").GetProperty("page_info");
        pageInfo.GetProperty("has_next_page").GetBoolean().ShouldBeTrue();
        DateTime nextCreatedAtUtc = pageInfo.GetProperty("next_created_at_utc").GetDateTime();
        Guid nextId = pageInfo.GetProperty("next_id").GetGuid();

        commandCounter.GetCommandTexts().Any(command =>
            command.Contains("COUNT(", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();

        string secondPageUrl = $"audit-logs/cursor?pageSize=1&afterCreatedAtUtc={nextCreatedAtUtc:O}&afterId={nextId}";
        HttpResponseMessage secondResponse = await HttpClient.GetAsync(secondPageUrl);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var second = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
        Guid secondId = second.RootElement.GetProperty("data").GetProperty("items")[0].GetProperty("id").GetGuid();

        secondId.ShouldNotBe(firstId);
    }

    [Fact]
    public async Task CursorEndpoint_ShouldRejectIncompleteCursor()
    {
        await AuthenticateAsAdministratorAsync();

        HttpResponseMessage response = await HttpClient.GetAsync(
            $"audit-logs/cursor?pageSize=20&afterId={Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
