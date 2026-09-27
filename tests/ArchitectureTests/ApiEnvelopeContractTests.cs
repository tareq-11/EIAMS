using System.Text.Json;
using Shouldly;
using Web.Api.Infrastructure;

namespace ArchitectureTests;

public sealed class ApiEnvelopeContractTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
    private static readonly string[] ErrorFields = ["code", "message", "details", "request_id"];
    private static readonly string[] InvalidFieldMessages = ["The submitted value is invalid."];
    private static readonly string[] OffsetPaginationFields =
        ["has_next_page", "has_previous_page", "page", "page_size", "total_items", "total_pages"];
    private static readonly string[] CursorPaginationFields =
        ["has_next_page", "has_previous_page", "mode", "next_created_at_utc", "next_id", "page", "page_size"];

    [Fact]
    public void SuccessEnvelope_ShouldKeepTopLevelDataPaginationAndMeta()
    {
        ResourceIdResponse resource = new(Guid.Parse("00000000-0000-0000-0000-00000000000b"));
        object?[] samples = new object?[]
        {
            new ApiResponse<ResourceIdResponse>(true, resource, null, Meta()),
            new ApiResponse<object>(true, new { name = "object" }, null, Meta()),
            new ApiResponse<IReadOnlyList<string>>(true, ["a", "b"], null, Meta()),
            new ApiResponse<IReadOnlyList<string>>(true, ["a", "b"], new ApiPagination(1, 20, 2, 1), Meta()),
            new ApiResponse<EmptyResponse>(true, null, null, Meta())
        };

        foreach (object? sample in samples)
        {
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(sample, sample!.GetType(), WebJson));
            JsonElement root = document.RootElement;
            root.EnumerateObject().Select(property => property.Name).ShouldBe(["success", "data", "pagination", "meta"]);
            root.GetProperty("success").GetBoolean().ShouldBeTrue();
            root.GetProperty("meta").GetProperty("request_id").GetString().ShouldNotBeNullOrWhiteSpace();
            root.ToString().ShouldNotContain("pageInfo", Case.Insensitive);
        }
    }

    [Fact]
    public void EmptySuccessEnvelope_ShouldRepresentTheCurrent200Response()
    {
        ApiResponse<EmptyResponse> response = new(true, null, null, Meta());

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response, WebJson));
        document.RootElement.GetProperty("success").GetBoolean().ShouldBeTrue();
        document.RootElement.GetProperty("data").ValueKind.ShouldBe(JsonValueKind.Null);
        document.RootElement.GetProperty("pagination").ValueKind.ShouldBe(JsonValueKind.Null);
        document.RootElement.GetProperty("meta").GetProperty("request_id").GetString().ShouldBe("request-0b");
    }

    [Fact]
    public void Pagination_ShouldRemainTopLevelWithCurrentSnakeCaseFields()
    {
        ApiResponse<IReadOnlyList<string>> response = new(
            true,
            ["row"],
            new ApiPagination(1, 20, 1, 1),
            Meta());

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response, WebJson));
        JsonElement root = document.RootElement;
        JsonElement pagination = root.GetProperty("pagination");
        pagination.GetProperty("page").GetInt32().ShouldBe(1);
        pagination.GetProperty("page_size").GetInt32().ShouldBe(20);
        pagination.GetProperty("total_items").GetInt32().ShouldBe(1);
        pagination.GetProperty("total_pages").GetInt32().ShouldBe(1);
        pagination.EnumerateObject().Select(property => property.Name).OrderBy(name => name).ToArray()
            .ShouldBe(OffsetPaginationFields);
        pagination.TryGetProperty("total_count", out _).ShouldBeFalse();
        pagination.TryGetProperty("mode", out _).ShouldBeFalse();
        pagination.TryGetProperty("next_created_at_utc", out _).ShouldBeFalse();
        pagination.TryGetProperty("next_id", out _).ShouldBeFalse();
        root.GetProperty("data").ValueKind.ShouldBe(JsonValueKind.Array);
        root.TryGetProperty("pageInfo", out _).ShouldBeFalse();
        root.ToString().ShouldNotContain("pageInfo", Case.Insensitive);
    }

    [Fact]
    public void CursorPagination_ShouldKeepCursorFieldsAtTopLevel()
    {
        ApiResponse<IReadOnlyList<string>> response = new(
            true,
            ["row"],
            new ApiPagination(
                Page: 1,
                PageSize: 20,
                TotalItems: null,
                TotalPages: null,
                HasPreviousPage: false,
                HasNextPage: true,
                Mode: "cursor",
                NextCreatedAtUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                NextId: Guid.Parse("00000000-0000-0000-0000-00000000000b")),
            Meta());

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response, WebJson));
        JsonElement pagination = document.RootElement.GetProperty("pagination");
        pagination.EnumerateObject().Select(property => property.Name).OrderBy(name => name).ToArray()
            .ShouldBe(CursorPaginationFields);
        pagination.GetProperty("mode").GetString().ShouldBe("cursor");
        pagination.GetProperty("next_id").GetGuid().ShouldNotBe(Guid.Empty);
        document.RootElement.TryGetProperty("data", out _).ShouldBeTrue();
        document.RootElement.ToString().ShouldNotContain("pageInfo", Case.Insensitive);
    }

    [Fact]
    public void ErrorEnvelope_ShouldExposeOnlyTheEstablishedShape()
    {
        ApiErrorResponse response = new(
            false,
            new ApiError(
                "REQUEST_VALIDATION_FAILED",
                "One or more request values are invalid.",
                new Dictionary<string, object?> { ["field"] = InvalidFieldMessages },
                "request-0b"));

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response, WebJson));
        JsonElement root = document.RootElement;
        root.EnumerateObject().Select(property => property.Name).ShouldBe(["success", "error"]);
        root.GetProperty("success").GetBoolean().ShouldBeFalse();
        JsonElement error = root.GetProperty("error");
        error.EnumerateObject().Select(property => property.Name).ShouldBe(ErrorFields);
        document.RootElement.ToString().ShouldNotContain("ProblemDetails", Case.Insensitive);
        document.RootElement.ToString().ShouldNotContain("System.", Case.Insensitive);
        document.RootElement.ToString().ShouldNotContain("pageInfo", Case.Insensitive);
    }

    private static ApiResponseMeta Meta() => new("request-0b", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
}
