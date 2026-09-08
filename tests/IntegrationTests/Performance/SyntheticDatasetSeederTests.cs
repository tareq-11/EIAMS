using Domain.Common;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Performance;

[Collection(nameof(IntegrationTestCollection))]
public sealed class SyntheticDatasetSeederTests
{
    private const long Seed = 20260908;
    private readonly IntegrationTestWebAppFactory factory;

    public SyntheticDatasetSeederTests(IntegrationTestWebAppFactory factory)
    {
        this.factory = factory;
    }

    [ExplicitSyntheticDatasetSeedFact]
    [Trait("Category", "Performance")]
    public async Task SeedSmallAsync_Should_CoalesceConcurrentCallsAndCreateValidIdempotentSmallDataset()
    {
        // Arrange
        const string databaseName = "clean_architecture_integration_test";
        var options = new SyntheticDatasetSeedOptions(
            factory.DatabaseConnectionString,
            databaseName,
            "Test",
            Seed);
        SyntheticDatasetManifest manifest = SyntheticDatasetManifestFactory.Create(DatasetProfile.Small, Seed);

        // Act
        SyntheticDatasetSeedResult[] results = await Task.WhenAll(
            SyntheticDatasetSeeder.SeedSmallAsync(options),
            SyntheticDatasetSeeder.SeedSmallAsync(options));
        SyntheticDatasetSeedResult first = results.Single(result => !result.WasAlreadySeeded);
        SyntheticDatasetSeedResult second = results.Single(result => result.WasAlreadySeeded);

        // Assert
        first.OperationalMovementCount.ShouldBe(manifest.Definition.OperationalMovementCount);
        first.AuditRecordCount.ShouldBe(manifest.Definition.AuditRecordCount);
        first.WasAlreadySeeded.ShouldBeFalse();
        second.OperationalMovementCount.ShouldBe(manifest.Definition.OperationalMovementCount);
        second.AuditRecordCount.ShouldBe(manifest.Definition.AuditRecordCount);
        second.WasAlreadySeeded.ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(() =>
            SyntheticDatasetSeeder.SeedSmallAsync(options with { Seed = Seed + 1 }));

        SyntheticDatasetManifest sameManifest = SyntheticDatasetManifestFactory.Create(DatasetProfile.Small, Seed);
        SyntheticDatasetManifest differentManifest = SyntheticDatasetManifestFactory.Create(DatasetProfile.Small, Seed + 1);
        SyntheticDatasetSeeder.ComputeManifestHash(sameManifest).ShouldNotBe(
            SyntheticDatasetSeeder.ComputeManifestHash(differentManifest));

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        int seededMovementCount = await context.StockMovements.CountAsync(movement =>
            movement.PostedAtUtc >= new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) &&
            movement.PostedAtUtc < new DateTime(2025, 1, 1, 0, 16, 40, DateTimeKind.Utc));
        seededMovementCount.ShouldBe(manifest.Definition.OperationalMovementCount);

        int movementsWithValidDocumentAndLine = await (
            from movement in context.StockMovements
            join document in context.WarehouseDocuments on movement.DocumentId equals document.Id
            join line in context.DocumentLines on new { movement.LineId, movement.DocumentId, movement.MaterialId }
                equals new { LineId = line.Id, line.DocumentId, line.MaterialId }
            where document.DocumentStatus == DocumentStatus.Posted
            select movement.Id).CountAsync();
        movementsWithValidDocumentAndLine.ShouldBe(manifest.Definition.OperationalMovementCount);

        List<(Guid UserId, ScopeType ScopeType, Guid? ScopeId)> assignments = await context.UserRoleScopes
            .Where(assignment => manifest.UserScopeAssignments.Select(expected => expected.UserId).Contains(assignment.UserId))
            .Select(assignment => new ValueTuple<Guid, ScopeType, Guid?>(
                assignment.UserId,
                assignment.ScopeType,
                assignment.ScopeId))
            .ToListAsync();
        assignments.Count.ShouldBe(manifest.Definition.UserCount);
        assignments.Select(assignment => assignment.UserId).Distinct().Count().ShouldBe(manifest.Definition.UserCount);
        assignments.Select(assignment => assignment.ScopeType).Distinct().Order().ShouldBe(
            Enum.GetValues<ScopeType>().Order());
        foreach ((Guid _, ScopeType scopeType, Guid? scopeId) in assignments)
        {
            if (scopeType == ScopeType.Enterprise)
            {
                scopeId.ShouldBeNull();
            }
            else
            {
                scopeId.ShouldNotBeNull();
                bool scopeResourceExists = scopeType switch
                {
                    ScopeType.Site => await context.Sites.AnyAsync(site => site.Id == scopeId.Value),
                    ScopeType.OrganizationalUnit => await context.OrganizationalUnits.AnyAsync(unit => unit.Id == scopeId.Value),
                    ScopeType.Warehouse => await context.Warehouses.AnyAsync(warehouse => warehouse.Id == scopeId.Value),
                    _ => false
                };
                scopeResourceExists.ShouldBeTrue();
            }
        }
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ExplicitSyntheticDatasetSeedFactAttribute : FactAttribute
{
    public ExplicitSyntheticDatasetSeedFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("RUN_SYNTHETIC_DATASET_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Explicit synthetic dataset seed test. Set RUN_SYNTHETIC_DATASET_TESTS=1 to run it.";
        }
    }
}
