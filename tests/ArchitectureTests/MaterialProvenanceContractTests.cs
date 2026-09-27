using System.Text.Json;
using Shouldly;

namespace ArchitectureTests;

/// <summary>
/// 3B gates that do not need a database: the public material API must expose semantic string
/// classification enums instead of integers, and every production document-line write must capture
/// the source-material provenance snapshot that submit/post re-validates.
/// </summary>
public sealed class MaterialProvenanceContractTests
{
    [Fact]
    public void CheckedInOpenApi_ShouldExposeSemanticStringClassificationEnumsOnMaterialWrites()
    {
        string snapshot = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "contracts", "openapi", "eiams-backend-v1.openapi.json"));
        using var document = JsonDocument.Parse(snapshot);
        JsonElement schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        FindSchema(schemas, "Domain.Materials.MaterialKind").GetProperty("enum")
            .EnumerateArray().Select(value => value.GetString())
            .ShouldBe(["Consumable", "Durable", "Asset"]);
        FindSchema(schemas, "Domain.Materials.TrackingType").GetProperty("enum")
            .EnumerateArray().Select(value => value.GetString())
            .ShouldBe(["Quantity", "Serial"]);

        foreach (string requestSchema in new[]
        {
            "Web.Api.Controllers.Materials.CreateMaterialController-RequestBody",
            "Web.Api.Controllers.Materials.UpdateMaterialController-RequestBody"
        })
        {
            JsonElement properties = FindSchema(schemas, requestSchema).GetProperty("properties");
            properties.GetProperty("materialKind").GetProperty("$ref").GetString()!
                .ShouldContain("Domain.Materials.MaterialKind");
            properties.GetProperty("trackingType").GetProperty("$ref").GetString()!
                .ShouldContain("Domain.Materials.TrackingType");
        }
    }

    [Fact]
    public void ProductionDocumentLineWrites_ShouldAlwaysCaptureSourceProvenance()
    {
        string applicationRoot = Path.Combine(FindRepositoryRoot(), "src", "Application");
        var missingProvenance = new List<string>();

        foreach (string file in Directory.EnumerateFiles(applicationRoot, "*.cs", SearchOption.AllDirectories))
        {
            string contents = File.ReadAllText(file);
            foreach ((string arguments, int line) in EnumerateInvocations(contents, "DocumentLine.Create("))
            {
                if (!arguments.Contains("rovenance", StringComparison.Ordinal))
                {
                    missingProvenance.Add(
                        $"{Path.GetRelativePath(FindRepositoryRoot(), file)}:{line}");
                }
            }
        }

        missingProvenance.ShouldBeEmpty(
            "every production DocumentLine.Create call must pass a DocumentLineProvenance argument");
    }

    [Fact]
    public void Material_ShouldNotDependOnFamilyBaseUnitForQuantitySemantics()
    {
        // Decision 038/3C: the family property has been removed; no quantity path may reintroduce it.
        string applicationRoot = Path.Combine(FindRepositoryRoot(), "src", "Application");
        var familyBaseUnitReaders = new List<string>();

        foreach (string file in Directory.EnumerateFiles(applicationRoot, "*.cs", SearchOption.AllDirectories))
        {
            string contents = File.ReadAllText(file);
            int index = contents.IndexOf("Family.BaseUnitId", StringComparison.Ordinal);
            if (index >= 0)
            {
                familyBaseUnitReaders.Add(
                    $"{Path.GetRelativePath(FindRepositoryRoot(), file)}:{LineNumberAt(contents, index)}");
            }
        }

        familyBaseUnitReaders.ShouldBeEmpty(
            "document-line and adjustment quantity paths must read Material.BaseUnitId, not the family value");
    }

    private static IEnumerable<(string Arguments, int Line)> EnumerateInvocations(
        string contents,
        string methodCall)
    {
        int searchIndex = 0;
        while ((searchIndex = contents.IndexOf(methodCall, searchIndex, StringComparison.Ordinal)) >= 0)
        {
            int openIndex = searchIndex + methodCall.Length - 1;
            int depth = 0;
            int index = openIndex;
            for (; index < contents.Length; index++)
            {
                if (contents[index] == '(')
                {
                    depth++;
                }
                else if (contents[index] == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        break;
                    }
                }
            }

            string arguments = contents[openIndex..Math.Min(index + 1, contents.Length)];
            int line = LineNumberAt(contents, searchIndex);
            yield return (arguments, line);
            searchIndex = index + 1;
        }
    }

    private static int LineNumberAt(string contents, int index) =>
        contents[..index].Split('\n').Length;

    private static JsonElement FindSchema(JsonElement schemas, string name) =>
        schemas.EnumerateObject()
            .First(property => property.Name == name ||
                property.Name.EndsWith(name, StringComparison.Ordinal))
            .Value;

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CleanArchitectureTemplate.slnx")))
        {
            directory = directory.Parent;
        }

        return (directory ?? throw new InvalidOperationException("Repository root was not found.")).FullName;
    }
}
