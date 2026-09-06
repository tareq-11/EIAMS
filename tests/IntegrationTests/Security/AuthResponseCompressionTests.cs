using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Security;

[Collection(nameof(IntegrationTestCollection))]
public sealed class AuthResponseCompressionTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task LoginResponse_Should_NotBeCompressed_WhenClientAcceptsGzip()
    {
        using HttpClient client = factory.CreateClient();
        client.BaseAddress = new Uri("http://localhost/api/v1/");
        using var request = new HttpRequestMessage(HttpMethod.Post, "auth/login")
        {
            Content = JsonContent.Create(new
            {
                email = $"missing-{Guid.NewGuid():N}@example.com",
                password = "InvalidPassword1!"
            })
        };
        request.Headers.AcceptEncoding.ParseAdd("gzip");

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentEncoding.ShouldBeEmpty();
        response.Headers.CacheControl?.NoStore.ShouldBeTrue();
    }
}
