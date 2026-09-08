using Domain.Common;

namespace IntegrationTests.Performance;

public sealed class SyntheticDatasetManifestTests
{
    [Theory]
    [InlineData(DatasetProfile.Small, 1_000, 1_000)]
    [InlineData(DatasetProfile.Medium, 100_000, 100_000)]
    [InlineData(DatasetProfile.Large, 1_000_000, 1_000_000)]
    public void Create_ShouldExposeExpectedProfileCounts(
        DatasetProfile profile,
        int expectedMovements,
        int expectedAuditRecords)
    {
        // Act
        SyntheticDatasetManifest manifest = SyntheticDatasetManifestFactory.Create(profile, seed: 1729);

        // Assert
        manifest.Definition.OperationalMovementCount.ShouldBe(expectedMovements);
        manifest.Definition.AuditRecordCount.ShouldBe(expectedAuditRecords);
        manifest.Sites.Count.ShouldBe(manifest.Definition.SiteCount);
        manifest.Sites.Sum(site => site.OrganizationalUnitIds.Count).ShouldBe(manifest.Definition.OrganizationalUnitCount);
        manifest.Sites.Sum(site => site.Warehouses.Count).ShouldBe(manifest.Definition.WarehouseCount);
    }

    [Fact]
    public void Create_ShouldBeDeterministicAndMaintainStructuralRelationships()
    {
        // Act
        SyntheticDatasetManifest first = SyntheticDatasetManifestFactory.Create(DatasetProfile.Small, 1729);
        SyntheticDatasetManifest second = SyntheticDatasetManifestFactory.Create(DatasetProfile.Small, 1729);

        // Assert
        first.Sites.Select(site => new
        {
            site.OrganizationId,
            site.SiteId,
            Units = string.Join(',', site.OrganizationalUnitIds),
            Warehouses = string.Join(',', site.Warehouses)
        }).ShouldBe(second.Sites.Select(site => new
        {
            site.OrganizationId,
            site.SiteId,
            Units = string.Join(',', site.OrganizationalUnitIds),
            Warehouses = string.Join(',', site.Warehouses)
        }));
        first.GetOperationalMovementId(999).ShouldBe(second.GetOperationalMovementId(999));
        first.GetAuditRecordId(999).ShouldBe(second.GetAuditRecordId(999));
        first.UserScopeAssignments.ShouldBe(second.UserScopeAssignments);
        first.Sites.All(site =>
            new[] { first.GetOrganizationId(0), first.GetOrganizationId(1) }.Contains(site.OrganizationId)).ShouldBeTrue();
        first.Sites.All(site => site.OrganizationalUnitIds.Count > 0 && site.Warehouses.Count > 0).ShouldBeTrue();
    }

    [Fact]
    public void Create_Large_ShouldNotMaterializeOperationalMovementOrAuditRows()
    {
        // Act
        SyntheticDatasetManifest manifest = SyntheticDatasetManifestFactory.Create(DatasetProfile.Large, 1729);

        // Assert
        manifest.Sites.Count.ShouldBeLessThan(100);
        manifest.Sites.SelectMany(site => site.Warehouses).Count().ShouldBe(240);
        manifest.GetOperationalMovementId(999_999).ShouldNotBe(Guid.Empty);
        manifest.GetAuditRecordId(999_999).ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Create_ShouldUseUnevenWarehouseDistribution()
    {
        // Act
        SyntheticDatasetManifest manifest = SyntheticDatasetManifestFactory.Create(DatasetProfile.Medium, 1729);

        // Assert
        manifest.Sites.Select(site => site.Warehouses.Count).Distinct().Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Create_ShouldAssignOneValidFixedScopePerUserAndLinkWarehousesToTheirSiteUnits()
    {
        // Arrange
        SyntheticDatasetManifest manifest = SyntheticDatasetManifestFactory.Create(DatasetProfile.Medium, 1729);
        Guid[] siteIds = manifest.Sites.Select(site => site.SiteId).ToArray();
        Guid[] organizationalUnitIds = manifest.Sites.SelectMany(site => site.OrganizationalUnitIds).ToArray();
        Guid[] warehouseIds = manifest.Sites.SelectMany(site => site.Warehouses).Select(warehouse => warehouse.WarehouseId).ToArray();

        // Assert
        manifest.UserScopeAssignments.Count.ShouldBe(manifest.Definition.UserCount);
        manifest.UserScopeAssignments.Select(assignment => assignment.UserId).Distinct().Count()
            .ShouldBe(manifest.Definition.UserCount);
        Enum.GetValues<ScopeType>().All(scopeType =>
            manifest.UserScopeAssignments.Any(assignment => assignment.ScopeType == scopeType)).ShouldBeTrue();
        manifest.UserScopeAssignments.All(assignment => assignment.RoleId != Guid.Empty &&
            manifest.RoleIds.Contains(assignment.RoleId)).ShouldBeTrue();
        manifest.UserScopeAssignments.All(assignment => assignment.ScopeType == ScopeType.Enterprise
            ? assignment.ScopeId is null
            : assignment.ScopeId is not null).ShouldBeTrue();
        manifest.UserScopeAssignments.Where(assignment => assignment.ScopeType == ScopeType.Site)
            .All(assignment => siteIds.Contains(assignment.ScopeId!.Value)).ShouldBeTrue();
        manifest.UserScopeAssignments.Where(assignment => assignment.ScopeType == ScopeType.OrganizationalUnit)
            .All(assignment => organizationalUnitIds.Contains(assignment.ScopeId!.Value)).ShouldBeTrue();
        manifest.UserScopeAssignments.Where(assignment => assignment.ScopeType == ScopeType.Warehouse)
            .All(assignment => warehouseIds.Contains(assignment.ScopeId!.Value)).ShouldBeTrue();
        manifest.Sites.All(site => site.Warehouses.All(warehouse =>
            site.OrganizationalUnitIds.Contains(warehouse.OrganizationalUnitId))).ShouldBeTrue();
    }

    [Fact]
    public void Create_ShouldRejectInvalidProfileSeedAndDefinition()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            SyntheticDatasetManifestFactory.Create((DatasetProfile)999, 1729));
        Should.Throw<ArgumentOutOfRangeException>(() =>
            SyntheticDatasetManifestFactory.Create(DatasetProfile.Small, 0));
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new SyntheticDatasetProfileDefinition(0, 1, 1, 1, 1, 1, 1, 1).Validate());
    }
}
