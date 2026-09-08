using System.Security.Cryptography;
using System.Text;
using Domain.Common;

namespace IntegrationTests.Performance;

public enum DatasetProfile
{
    Small,
    Medium,
    Large
}

internal sealed record SyntheticDatasetProfileDefinition(
    int OperationalMovementCount,
    int AuditRecordCount,
    int OrganizationCount,
    int SiteCount,
    int OrganizationalUnitCount,
    int WarehouseCount,
    int UserCount,
    int MaterialCount)
{
    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(OperationalMovementCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(AuditRecordCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(OrganizationCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(SiteCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(OrganizationalUnitCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(WarehouseCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(UserCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaterialCount);

        if (SiteCount < OrganizationCount || OrganizationalUnitCount < SiteCount || WarehouseCount < SiteCount ||
            OrganizationalUnitCount % SiteCount != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SiteCount));
        }
    }
}

internal static class SyntheticDatasetManifestFactory
{
    private static readonly Dictionary<DatasetProfile, SyntheticDatasetProfileDefinition> Definitions =
        new Dictionary<DatasetProfile, SyntheticDatasetProfileDefinition>
        {
            [DatasetProfile.Small] = new(1_000, 1_000, 2, 6, 18, 12, 24, 40),
            [DatasetProfile.Medium] = new(100_000, 100_000, 4, 20, 80, 48, 160, 500),
            [DatasetProfile.Large] = new(1_000_000, 1_000_000, 8, 64, 320, 240, 1_200, 5_000)
        };

    internal static SyntheticDatasetManifest Create(DatasetProfile profile, long seed)
    {
        if (!Definitions.TryGetValue(profile, out SyntheticDatasetProfileDefinition? definition))
        {
            throw new ArgumentOutOfRangeException(nameof(profile));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seed);

        definition.Validate();
        List<SyntheticDatasetSite> sites = CreateSites(profile, seed, definition);
        Guid[] roleIds = Enumerable.Range(0, 4)
            .Select(index => DeterministicGuid(profile, seed, "role", index))
            .ToArray();
        List<SyntheticUserScopeAssignment> assignments = CreateAssignments(profile, seed, definition, sites, roleIds);
        return new SyntheticDatasetManifest(profile, seed, definition, sites, roleIds, assignments);
    }

    private static List<SyntheticDatasetSite> CreateSites(
        DatasetProfile profile,
        long seed,
        SyntheticDatasetProfileDefinition definition)
    {
        int[] warehouseCounts = AllocateUnevenWarehouseCounts(definition.SiteCount, definition.WarehouseCount);
        int unitsPerSite = definition.OrganizationalUnitCount / definition.SiteCount;
        List<SyntheticDatasetSite> sites = new(definition.SiteCount);
        int warehouseIndex = 0;

        for (int siteIndex = 0; siteIndex < definition.SiteCount; siteIndex++)
        {
            Guid organizationId = DeterministicGuid(profile, seed, "organization", siteIndex % definition.OrganizationCount);
            Guid siteId = DeterministicGuid(profile, seed, "site", siteIndex);
            var unitIds = new List<Guid>(unitsPerSite);
            for (int unitOffset = 0; unitOffset < unitsPerSite; unitOffset++)
            {
                unitIds.Add(DeterministicGuid(profile, seed, "organizational-unit", siteIndex * unitsPerSite + unitOffset));
            }

            var warehouses = new List<SyntheticDatasetWarehouse>(warehouseCounts[siteIndex]);
            for (int warehouseOffset = 0; warehouseOffset < warehouseCounts[siteIndex]; warehouseOffset++)
            {
                warehouses.Add(new SyntheticDatasetWarehouse(
                    DeterministicGuid(profile, seed, "warehouse", warehouseIndex),
                    unitIds[warehouseOffset % unitIds.Count]));
                warehouseIndex++;
            }

            sites.Add(new SyntheticDatasetSite(organizationId, siteId, unitIds, warehouses));
        }

        return sites;
    }

    private static int[] AllocateUnevenWarehouseCounts(int siteCount, int warehouseCount)
    {
        int[] counts = Enumerable.Repeat(1, siteCount).ToArray();
        int remaining = warehouseCount - siteCount;

        for (int allocation = 0; allocation < remaining; allocation++)
        {
            int siteIndex = allocation % Math.Min(siteCount, 3);
            counts[siteIndex]++;
        }

        return counts;
    }

    private static List<SyntheticUserScopeAssignment> CreateAssignments(
        DatasetProfile profile,
        long seed,
        SyntheticDatasetProfileDefinition definition,
        IReadOnlyList<SyntheticDatasetSite> sites,
        Guid[] roleIds)
    {
        Guid[] siteIds = sites.Select(site => site.SiteId).ToArray();
        Guid[] organizationalUnitIds = sites.SelectMany(site => site.OrganizationalUnitIds).ToArray();
        Guid[] warehouseIds = sites.SelectMany(site => site.Warehouses).Select(warehouse => warehouse.WarehouseId).ToArray();
        ScopeType[] scopeTypes = Enum.GetValues<ScopeType>();
        var assignments = new List<SyntheticUserScopeAssignment>(definition.UserCount);

        for (int userIndex = 0; userIndex < definition.UserCount; userIndex++)
        {
            ScopeType scopeType = scopeTypes[userIndex % scopeTypes.Length];
            Guid? scopeId = scopeType switch
            {
                ScopeType.Enterprise => null,
                ScopeType.Site => siteIds[userIndex % siteIds.Length],
                ScopeType.OrganizationalUnit => organizationalUnitIds[userIndex % organizationalUnitIds.Length],
                ScopeType.Warehouse => warehouseIds[userIndex % warehouseIds.Length],
                _ => throw new InvalidOperationException("The generated scope type is not supported.")
            };

            assignments.Add(new SyntheticUserScopeAssignment(
                DeterministicGuid(profile, seed, "user", userIndex),
                roleIds[userIndex % roleIds.Length],
                scopeType,
                scopeId));
        }

        return assignments;
    }

    internal static Guid DeterministicGuid(DatasetProfile profile, long seed, string kind, int index)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{profile}:{seed}:{kind}:{index}"));
        return new Guid(bytes.AsSpan(0, 16));
    }
}

internal sealed class SyntheticDatasetManifest(
    DatasetProfile profile,
    long seed,
    SyntheticDatasetProfileDefinition definition,
    IReadOnlyList<SyntheticDatasetSite> sites,
    IReadOnlyList<Guid> roleIds,
    IReadOnlyList<SyntheticUserScopeAssignment> userScopeAssignments)
{
    internal DatasetProfile Profile { get; } = profile;

    internal long Seed { get; } = seed;

    internal SyntheticDatasetProfileDefinition Definition { get; } = definition;

    internal IReadOnlyList<SyntheticDatasetSite> Sites { get; } = sites;

    internal IReadOnlyList<Guid> RoleIds { get; } = roleIds;

    internal IReadOnlyList<SyntheticUserScopeAssignment> UserScopeAssignments { get; } = userScopeAssignments;

    internal Guid GetOrganizationId(int index) => GetId("organization", index, Definition.OrganizationCount);

    internal Guid GetUserId(int index) => GetId("user", index, Definition.UserCount);

    internal Guid GetMaterialId(int index) => GetId("material", index, Definition.MaterialCount);

    internal Guid GetOperationalMovementId(int index) => GetId("movement", index, Definition.OperationalMovementCount);

    internal Guid GetAuditRecordId(int index) => GetId("audit", index, Definition.AuditRecordCount);

    private Guid GetId(string kind, int index, int count)
    {
        if ((uint)index >= (uint)count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return SyntheticDatasetManifestFactory.DeterministicGuid(Profile, Seed, kind, index);
    }
}

internal sealed record SyntheticDatasetSite(
    Guid OrganizationId,
    Guid SiteId,
    IReadOnlyList<Guid> OrganizationalUnitIds,
    IReadOnlyList<SyntheticDatasetWarehouse> Warehouses);

internal sealed record SyntheticDatasetWarehouse(Guid WarehouseId, Guid OrganizationalUnitId);

internal sealed record SyntheticUserScopeAssignment(
    Guid UserId,
    Guid RoleId,
    ScopeType ScopeType,
    Guid? ScopeId);
