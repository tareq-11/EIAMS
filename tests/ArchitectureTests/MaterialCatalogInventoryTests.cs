using Shouldly;

namespace ArchitectureTests;

public sealed class MaterialCatalogInventoryTests
{
    private static readonly string[] RequiredInventoryPaths =
    [
        "src/Domain/Materials/Material.cs",
        "src/Infrastructure/Materials/MaterialConfiguration.cs",
        "src/Domain/MaterialFamilies/MaterialFamily.cs",
        "src/Infrastructure/MaterialFamilies/MaterialFamilyConfiguration.cs",
        "src/Application/DocumentLines/DocumentLineCatalogResolver.cs",
        "src/Application/DocumentLines/BaseQuantityCalculator.cs",
        "src/Application/MaterialUnitConversions/Add/AddMaterialUnitConversionCommandHandler.cs",
        "src/Application/InventoryAdjustments/CreateDisposal/CreateDisposalCommandHandler.cs",
        "src/Application/InventoryAdjustments/CreateFromCount/CreateAdjustmentFromCountCommandHandler.cs",
        "src/Infrastructure/WarehouseDocuments/PostingMaterialCatalogLoader.cs",
        "src/Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs",
        "src/Web.Api/Controllers/MaterialFamilies/CreateMaterialFamilyController.cs",
        "contracts/openapi/eiams-backend-v1.openapi.json",
        "tests/IntegrationTests/Performance/SyntheticDatasetSeeder.cs"
    ];

    [Fact]
    public void Phase3AInventory_ShouldNameEveryRequiredBlastRadiusAnchor()
    {
        string repositoryRoot = FindRepositoryRoot();
        string inventory = File.ReadAllText(Path.Combine(
            repositoryRoot, "docs", "phase3a-material-base-unit-inventory.md"));

        inventory.ShouldContain("inventory complete; 3A gate recorded");
        inventory.ShouldContain("Material.BaseUnitId");
        inventory.ShouldContain("MaterialFamily.BaseUnitId");
        inventory.ShouldContain("No migration file is modified");

        foreach (string path in RequiredInventoryPaths)
        {
            File.Exists(Path.Combine(repositoryRoot, path)).ShouldBeTrue(path);
            inventory.ShouldContain(path);
        }
    }

    [Fact]
    public void Phase3AInventory_ShouldContainEveryCurrentClassificationSourceAndTest()
    {
        string repositoryRoot = FindRepositoryRoot();
        string inventory = File.ReadAllText(Path.Combine(
            repositoryRoot, "docs", "phase3a-material-base-unit-inventory.md"));

        string[] roots =
        [
            Path.Combine(repositoryRoot, "src", "Domain"),
            Path.Combine(repositoryRoot, "src", "Application"),
            Path.Combine(repositoryRoot, "src", "Infrastructure"),
            Path.Combine(repositoryRoot, "src", "Web.Api"),
            Path.Combine(repositoryRoot, "tests")
        ];

        HashSet<string> domainDefinitionFiles =
        [
            "src/Domain/Materials/MaterialKind.cs",
            "src/Domain/Materials/TrackingType.cs",
            "src/Domain/Materials/Material.cs",
            "src/Domain/Common/DocumentLineType.cs"
        ];

        foreach (string root in roots)
        {
            foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
                if (relativePath.Contains("/Migrations/", StringComparison.Ordinal))
                {
                    continue; // Historical generated snapshots are immutable, not operational consumers.
                }

                if (domainDefinitionFiles.Contains(relativePath))
                {
                    continue; // Domain definition files DEFINE the vocabulary, not consume it.
                }

                string contents = File.ReadAllText(file);
                if (contents.Contains("MaterialKind", StringComparison.Ordinal)
                    || contents.Contains("TrackingType", StringComparison.Ordinal)
                    || contents.Contains("RequiresAssetNumber", StringComparison.Ordinal)
                    || contents.Contains("IsAssetTracked", StringComparison.Ordinal))
                {
                    inventory.ShouldContain(relativePath);
                }
            }
        }
    }

    [Fact]
    public void Phase3CProductionCode_ShouldNotModelOrReadFamilyBaseUnit()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] productionRoots =
        [
            Path.Combine(repositoryRoot, "src", "Domain"),
            Path.Combine(repositoryRoot, "src", "Application"),
            Path.Combine(repositoryRoot, "src", "Infrastructure"),
            Path.Combine(repositoryRoot, "src", "Web.Api")
        ];

        foreach (string root in productionRoots)
        {
            foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
                if (relativePath.Contains("/Migrations/", StringComparison.Ordinal))
                {
                    continue; // Historical migrations are immutable records of the old schema.
                }

                string contents = File.ReadAllText(file);
                contents.ShouldNotContain("MaterialFamily.BaseUnitId", customMessage: relativePath);
                if (relativePath.EndsWith("/MaterialFamily.cs", StringComparison.Ordinal))
                {
                    contents.ShouldNotContain("BaseUnitId", customMessage: relativePath);
                }
            }
        }
    }

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
