using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Web.Api.Infrastructure;

namespace IntegrationTests.Api;

public sealed class ApiResultsContractTests
{
    public static TheoryData<int, string> ErrorCases => new()
    {
        { StatusCodes.Status400BadRequest, "REQUEST_INVALID" },
        { StatusCodes.Status401Unauthorized, "AUTHENTICATION_REQUIRED" },
        { StatusCodes.Status403Forbidden, "AUTHORIZATION_FORBIDDEN" },
        { StatusCodes.Status404NotFound, "RESOURCE_NOT_FOUND" },
        { StatusCodes.Status409Conflict, "RESOURCE_CONFLICT" },
        { StatusCodes.Status500InternalServerError, "SERVER_FAILURE" }
    };

    [Theory]
    [MemberData(nameof(ErrorCases))]
    public async Task ErrorFromStatusCode_ShouldReturnTheEstablishedEnvelope(int statusCode, string expectedCode)
    {
        DefaultHttpContext context = new();
        context.Items[ApiRequestContext.RequestIdItemKey] = "request-0b-error";
        ServiceCollection services = new();
        services.AddLogging();
        services.AddOptions();
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(_ => { });
        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        context.RequestServices = serviceProvider;
        await using MemoryStream responseBody = new();
        context.Response.Body = responseBody;

        IResult result = ApiResults.ErrorFromStatusCode(context, statusCode);
        await result.ExecuteAsync(context);

        context.Response.StatusCode.ShouldBe(statusCode);
        context.Response.ContentType.ShouldStartWith("application/json");
        responseBody.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(responseBody);
        JsonElement root = document.RootElement;
        root.EnumerateObject().Select(property => property.Name).ShouldBe(["success", "error"]);
        root.GetProperty("success").GetBoolean().ShouldBeFalse();

        JsonElement error = root.GetProperty("error");
        error.EnumerateObject().Select(property => property.Name)
            .ShouldBe(["code", "message", "details", "request_id"]);
        error.GetProperty("code").GetString().ShouldBe(expectedCode);
        error.GetProperty("message").GetString().ShouldNotBeNullOrWhiteSpace();
        error.GetProperty("details").EnumerateObject().ShouldBeEmpty();
        error.GetProperty("request_id").GetString().ShouldBe("request-0b-error");
        string serialized = root.ToString();
        serialized.ShouldNotContain("Exception", Case.Insensitive);
        serialized.ShouldNotContain("System.", Case.Insensitive);
        serialized.ShouldNotContain("StackTrace", Case.Insensitive);
    }
}
