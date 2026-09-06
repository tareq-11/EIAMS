using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IntegrationTests.Security;

[Collection(nameof(IntegrationTestCollection))]
public sealed class JwtConfigurationTests : BaseIntegrationTest
{
    public JwtConfigurationTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task InvalidBearerToken_Should_Return401Unauthorized()
    {
        // Arrange: Provide invalid token
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.jwt.token");

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("organizations");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ValidToken_Should_Authenticate_ButStillRequirePermission()
    {
        // Arrange
        (_, AccessTokens tokens) = await RegisterAndLoginAsync();
        Authenticate(tokens.AccessToken);

        // Act
        HttpResponseMessage response = await HttpClient.GetAsync("admin/permissions");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SignedTokenWithoutUserSubject_Should_Return401InsteadOfReachingApplicationCode()
    {
        string token = CreateToken(subject: null, IntegrationTestWebAppFactory.JwtAudience, DateTime.UtcNow.AddMinutes(5));
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await HttpClient.GetAsync("organizations");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SignedTokenForWrongAudience_Should_Return401Unauthorized()
    {
        string token = CreateToken(Guid.NewGuid(), "another-audience", DateTime.UtcNow.AddMinutes(5));
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await HttpClient.GetAsync("organizations");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SignedTokenUsingUnapprovedAlgorithm_Should_Return401Unauthorized()
    {
        string token = CreateToken(
            Guid.NewGuid(),
            IntegrationTestWebAppFactory.JwtAudience,
            DateTime.UtcNow.AddMinutes(5),
            SecurityAlgorithms.HmacSha384);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await HttpClient.GetAsync("organizations");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SignedTokenWithEmptyUserSubject_Should_Return401Unauthorized()
    {
        string token = CreateToken(Guid.Empty, IntegrationTestWebAppFactory.JwtAudience, DateTime.UtcNow.AddMinutes(5));
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await HttpClient.GetAsync("organizations");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExpiredSignedToken_Should_Return401Unauthorized()
    {
        string token = CreateToken(Guid.NewGuid(), IntegrationTestWebAppFactory.JwtAudience, DateTime.UtcNow.AddSeconds(-1));
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await HttpClient.GetAsync("organizations");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static string CreateToken(
        Guid? subject,
        string audience,
        DateTime expires,
        string algorithm = SecurityAlgorithms.HmacSha256)
    {
        Claim[] claims = subject.HasValue
            ? [new Claim(JwtRegisteredClaimNames.Sub, subject.Value.ToString())]
            : [];
        var securityKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(IntegrationTestWebAppFactory.JwtSecret));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = IntegrationTestWebAppFactory.JwtIssuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(securityKey, algorithm)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
