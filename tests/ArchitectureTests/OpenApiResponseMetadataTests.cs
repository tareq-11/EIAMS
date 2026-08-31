using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Shouldly;
using Web.Api.Infrastructure;

namespace ArchitectureTests;

public sealed class OpenApiResponseMetadataTests : BaseTest
{
    private static readonly HashSet<string> RawResponseAllowlist =
    [
        "Web.Api.Controllers.DocumentAttachments.GetDocumentAttachmentContentController.Handle"
    ];

    [Fact]
    public void Every_Controller_Action_Should_Describe_Its_Success_Response()
    {
        var missingSuccessMetadata = GetControllerActions()
            .Where(action => !GetSuccessResponses(action).Any())
            .Select(GetActionName)
            .ToList();

        missingSuccessMetadata.ShouldBeEmpty(
            "Every API operation must expose an explicit 2xx response so generated clients can consume the contract.");
    }

    [Fact]
    public void Every_Json_Success_Response_Should_Use_The_Standard_Api_Response_Envelope()
    {
        var invalidSuccessMetadata = new List<string>();

        foreach (MethodInfo action in GetControllerActions())
        {
            string actionName = GetActionName(action);

            if (RawResponseAllowlist.Contains(actionName))
            {
                continue;
            }

            foreach (ProducesResponseTypeAttribute response in GetSuccessResponses(action))
            {
                Type? responseType = response.Type;
                bool usesApiEnvelope = responseType is not null &&
                                       responseType.IsGenericType &&
                                       responseType.GetGenericTypeDefinition() == typeof(ApiResponse<>);

                if (!usesApiEnvelope)
                {
                    invalidSuccessMetadata.Add(
                        $"{actionName} ({response.StatusCode}: {responseType?.FullName ?? "missing type"})");
                }
            }
        }

        invalidSuccessMetadata.ShouldBeEmpty(
            "JSON success responses must describe the same ApiResponse<T> envelope returned at runtime.");
    }

    [Fact]
    public void Resource_Id_Responses_Should_Not_Be_Described_As_Raw_Guids()
    {
        var rawGuidResponses = GetControllerActions()
            .SelectMany(action => GetSuccessResponses(action)
                .Select(response => (Action: action, Response: response)))
            .Where(item => item.Response.Type == typeof(ApiResponse<Guid>))
            .Select(item => GetActionName(item.Action))
            .ToList();

        rawGuidResponses.ShouldBeEmpty(
            "Result<Guid> is serialized as ApiResponse<ResourceIdResponse>, not ApiResponse<Guid>.");
    }

    [Fact]
    public void Raw_File_Responses_Should_Describe_Binary_Content()
    {
        var rawFileActions = GetControllerActions()
            .Where(action => RawResponseAllowlist.Contains(GetActionName(action)))
            .ToList();

        rawFileActions.Count.ShouldBe(RawResponseAllowlist.Count);

        var invalidRawResponses = rawFileActions
            .SelectMany(action => GetSuccessResponses(action)
                .Select(response => (Action: action, Response: response)))
            .Where(item => item.Response.Type != typeof(Stream))
            .Select(item => GetActionName(item.Action))
            .ToList();

        invalidRawResponses.ShouldBeEmpty(
            "Raw file downloads must use Stream response metadata so OpenAPI emits a binary schema.");
    }

    private static IEnumerable<MethodInfo> GetControllerActions() =>
        PresentationAssembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .SelectMany(type => type.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any());

    private static IEnumerable<ProducesResponseTypeAttribute> GetSuccessResponses(MethodInfo action) =>
        action.GetCustomAttributes<ProducesResponseTypeAttribute>(inherit: true)
            .Where(response => response.StatusCode is >= 200 and < 300);

    private static string GetActionName(MethodInfo action) =>
        $"{action.DeclaringType?.FullName}.{action.Name}";
}
