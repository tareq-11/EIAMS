using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedKernel;
using Web.Api.Infrastructure;

namespace IntegrationTests.Api;

public sealed class UnavailableErrorResponseTests
{
    [Fact]
    public async Task UnavailableError_ShouldUseStandard503Envelope()
    {
        var context = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(_ => { });
        context.RequestServices = services.BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        IResult result = CustomResults.Problem(Result.Failure(Error.Unavailable("DocumentAttachments.MalwareScannerUnavailable", "safe")), context);

        await result.ExecuteAsync(context);
        context.Response.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        context.Response.Body.Position = 0;
        using JsonDocument body = await JsonDocument.ParseAsync(context.Response.Body);
        body.RootElement.GetProperty("success").GetBoolean().ShouldBeFalse();
        body.RootElement.GetProperty("error").GetProperty("code").GetString().ShouldBe("DOCUMENT_ATTACHMENTS_MALWARE_SCANNER_UNAVAILABLE");
    }
}
