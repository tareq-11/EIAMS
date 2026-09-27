using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Web.Api;

namespace IntegrationTests.Api;

[Collection(nameof(IntegrationTestCollection))]
public sealed class OpenApiSnapshotTests(IntegrationTestWebAppFactory factory)
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };
    private static readonly string[] ApiPaginationProperties =
    [
        "has_next_page", "has_previous_page", "mode", "next_created_at_utc", "next_id", "page", "page_size",
        "total_count", "total_items", "total_pages"
    ];

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

    [Fact]
    public async Task CheckedInOpenApi_ShouldProtectTheCurrentEnvelopeAndTopLevelPagination()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        string snapshotPath = Path.Combine(repositoryRoot, "contracts", "openapi", "eiams-backend-v1.openapi.json");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(snapshotPath));
        JsonElement root = document.RootElement;
        string serialized = root.ToString();

        serialized.ShouldNotContain("pageInfo", Case.Insensitive);
        serialized.ShouldNotContain("activeRoles", Case.Insensitive);
        serialized.ShouldNotContain("scopeState", Case.Insensitive);
        serialized.ShouldNotContain("availableScopes", Case.Insensitive);
        JsonElement schemas = root.GetProperty("components").GetProperty("schemas");
        JsonElement apiResponse = FindSchema(schemas, "ApiResponse`1");
        apiResponse.GetProperty("properties").EnumerateObject().Select(property => property.Name)
            .ShouldBe(["success", "data", "pagination", "meta"]);
        JsonElement apiErrorResponse = FindSchema(schemas, "ApiErrorResponse");
        apiErrorResponse.GetProperty("properties").EnumerateObject().Select(property => property.Name)
            .ShouldBe(["success", "error"]);
        JsonElement apiError = FindSchema(schemas, "ApiError");
        apiError.GetProperty("properties").EnumerateObject().Select(property => property.Name)
            .ShouldBe(["code", "message", "details", "request_id"]);
        JsonElement pagination = FindSchema(schemas, "ApiPagination");
        pagination.GetProperty("properties").EnumerateObject().Select(property => property.Name).OrderBy(name => name)
            .ShouldBe(ApiPaginationProperties);
        pagination.GetProperty("properties").EnumerateObject().Select(property => property.Name)
            .ShouldNotContain("pageInfo");
        FindSchema(schemas, "AuthenticationTokensResponse").GetProperty("properties")
            .EnumerateObject().Select(property => property.Name)
            .ShouldNotContain("refreshToken");
        FindSchema(schemas, "AuthenticationTokensResponse").GetProperty("required")
            .EnumerateArray().Select(value => value.GetString())
            .ShouldNotContain("refreshToken");
        FindSchema(schemas, "UserAssignmentScopeType").GetProperty("enum")
            .EnumerateArray().Select(value => value.GetString()).ShouldBe(["Enterprise", "Site", "Warehouse"]);
        foreach (string requestSchema in new[]
        {
            "Web.Api.Controllers.UserRoleScopes.ReplaceAssignmentController-RequestBody",
            "Web.Api.Controllers.Roles.CreateRoleController-RequestBody",
            "Web.Api.Controllers.Roles.UpdateRoleController-RequestBody"
        })
        {
            bool isRole = requestSchema.Contains("RoleController", StringComparison.Ordinal);
            JsonElement scopeProperty = schemas.GetProperty(requestSchema).GetProperty("properties")
                .GetProperty(isRole ? "allowedScopeTypes" : "scopeType");
            string scopeReference = isRole
                ? scopeProperty.GetProperty("items").GetProperty("$ref").GetString()!
                : scopeProperty.GetProperty("$ref").GetString()!;
            scopeReference.ShouldContain("UserAssignmentScopeType");
        }

        foreach (JsonProperty path in root.GetProperty("paths").EnumerateObject())
        {
            foreach (JsonProperty operation in path.Value.EnumerateObject()
                         .Where(property => IsHttpMethod(property.Name)))
            {
                JsonElement responses = operation.Value.GetProperty("responses");
                foreach (JsonProperty response in responses.EnumerateObject())
                {
                    JsonElement content = response.Value.TryGetProperty("content", out JsonElement value)
                        ? value
                        : default;
                    if (content.ValueKind != JsonValueKind.Object ||
                        !content.TryGetProperty("application/json", out JsonElement json))
                    {
                        continue;
                    }

                    string reference = json.GetProperty("schema").GetProperty("$ref").GetString()!;
                    if (response.Name.StartsWith('2'))
                    {
                        reference.ShouldContain("ApiResponse");
                    }
                    else
                    {
                        reference.ShouldContain("ApiErrorResponse");
                    }
                }
            }
        }
    }

    [Fact]
    public void CheckedInOpenApi_ShouldExposeOnlyCanonicalRoleScopeRoute()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        string snapshotPath = Path.Combine(repositoryRoot, "contracts", "openapi", "eiams-backend-v1.openapi.json");
        using var document = JsonDocument.Parse(File.ReadAllText(snapshotPath));
        JsonElement paths = document.RootElement.GetProperty("paths");
        JsonElement canonical = paths.GetProperty("/api/v1/admin/users/{userId}/role-scope");
        canonical.EnumerateObject().Select(property => property.Name).OrderBy(name => name)
            .ShouldBe(["get", "put"]);
        paths.TryGetProperty("/api/v1/admin/user-role-scopes", out _).ShouldBeFalse();
        paths.TryGetProperty("/api/v1/admin/users/{userId}/role-scopes", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task CheckedInOpenApi_ShouldKeepBaseUnitOnMaterialsButNotFamilies()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        string snapshotPath = Path.Combine(repositoryRoot, "contracts", "openapi", "eiams-backend-v1.openapi.json");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(snapshotPath));
        JsonElement schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        foreach (string familySchema in new[]
        {
            "Application.MaterialFamilies.GetById.MaterialFamilyResponse",
            "Application.MaterialFamilies.GetList.MaterialFamilyResponse",
            "Web.Api.Controllers.MaterialFamilies.CreateMaterialFamilyController-RequestBody"
        })
        {
            JsonElement schema = schemas.GetProperty(familySchema);
            schema.GetProperty("properties").EnumerateObject().Select(property => property.Name)
                .ShouldNotContain("baseUnitId");
            if (schema.TryGetProperty("required", out JsonElement required))
            {
                required.EnumerateArray().Select(property => property.GetString()).ShouldNotContain("baseUnitId");
            }
        }

        schemas.GetProperty("Application.Materials.GetById.MaterialResponse").GetProperty("properties")
            .EnumerateObject().Select(property => property.Name).ShouldContain("baseUnitId");
        schemas.GetProperty("Application.Materials.GetList.MaterialResponse").GetProperty("properties")
            .EnumerateObject().Select(property => property.Name).ShouldContain("baseUnitId");
        schemas.GetProperty("Web.Api.Controllers.Materials.CreateMaterialController-RequestBody")
            .GetProperty("properties").EnumerateObject().Select(property => property.Name)
            .ShouldContain("baseUnitId");
    }

    [Fact]
    public async Task CheckedInOpenApi_ShouldUseExplicitExternalSupplierSelection()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(repositoryRoot, "contracts", "openapi", "eiams-backend-v1.openapi.json")));
        JsonElement schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        JsonElement receivingRequest = schemas.GetProperty(
            "Web.Api.Controllers.ReceivingInfos.UpsertReceivingInfoController-RequestBody");
        receivingRequest.GetProperty("properties").EnumerateObject().Select(property => property.Name)
            .ShouldContain("supplierPartyId");
        receivingRequest.GetProperty("properties").EnumerateObject().Select(property => property.Name)
            .ShouldNotContain("supplierRef");
        JsonElement supplier = schemas.GetProperty("Application.ReceivingInfos.SupplierSuggestionResponse");
        supplier.GetProperty("properties").EnumerateObject().Select(property => property.Name)
            .ShouldBe(["id", "nameAr", "code"]);

        JsonElement paths = document.RootElement.GetProperty("paths");
        paths.TryGetProperty("/api/v1/warehouse-documents/{documentId}/transfer-info", out _).ShouldBeTrue();
        paths.TryGetProperty("/api/v1/warehouse-documents/{documentId}/return-info", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task CheckedInOpenApi_ShouldExposeReversalIdempotencyKeyAndRowVersionForPost()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(repositoryRoot, "contracts", "openapi", "eiams-backend-v1.openapi.json")));
        JsonElement paths = document.RootElement.GetProperty("paths");
        JsonElement reversal = paths.GetProperty("/api/v1/warehouse-documents/{documentId}/reversals").GetProperty("post");
        reversal.GetProperty("parameters").EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString())
            .ShouldContain("Idempotency-Key");
        JsonElement post = paths.GetProperty("/api/v1/warehouse-documents/{documentId}/post").GetProperty("post");
        string postBodySchema = post.GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString()!;
        postBodySchema.ShouldContain("PostDocumentController-RequestBody");
        document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("Web.Api.Controllers.WarehouseDocuments.PostDocumentController-RequestBody")
            .GetProperty("properties").EnumerateObject().Select(property => property.Name)
            .ShouldContain("expectedRowVersion");
    }

    [Fact]
    public void CheckedInOpenApi_ShouldHaveUniqueOperationsAndCanonicalMaterialConversionRoutes()
    {
        string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(repositoryRoot, "contracts", "openapi", "eiams-backend-v1.openapi.json")));
        JsonElement paths = document.RootElement.GetProperty("paths");

        string?[] operationIds = paths.EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject()
                .Where(property => IsHttpMethod(property.Name))
                .Select(property => property.Value.GetProperty("operationId").GetString()))
            .Where(operationId => operationId is not null)
            .ToArray();
        operationIds.Length.ShouldBe(operationIds.Distinct(StringComparer.Ordinal).Count());

        paths.TryGetProperty("/api/v1/catalog/materials/{materialId}/unit-conversions", out _).ShouldBeTrue();
        paths.TryGetProperty(
            "/api/v1/catalog/materials/{materialId}/unit-conversions/{conversionId}", out _).ShouldBeTrue();
        paths.TryGetProperty("/api/v1/catalog/material-unit-conversions", out _).ShouldBeFalse();
    }

    private static JsonElement FindSchema(JsonElement schemas, string suffix) =>
        schemas.EnumerateObject()
            .First(property => suffix == "ApiResponse`1"
                ? property.Name.Contains(suffix, StringComparison.Ordinal)
                : property.Name.EndsWith(suffix, StringComparison.Ordinal))
            .Value;

    private static bool IsHttpMethod(string name) => name is
        "get" or "post" or "put" or "patch" or "delete" or "options" or "head" or "trace";
}
