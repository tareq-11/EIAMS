namespace Application.Materials.GetList;

public sealed class MaterialResponse
{
    public Guid Id { get; init; }

    public Guid FamilyId { get; init; }

    public string NameAr { get; init; }

    public string? NameEn { get; init; }

    public string Code { get; init; }

    public string MaterialKind { get; init; }

    public string TrackingType { get; init; }

    public bool HasExpiry { get; init; }

    public bool RequiresAssetNumber { get; init; }

    /// <summary>
    /// Classification revision the client must echo as <c>expectedCatalogVersion</c> when updating
    /// this material (3B optimistic concurrency). A stale value is rejected with a conflict instead of
    /// overwriting a classification change the client never observed.
    /// </summary>
    public int CatalogVersion { get; init; }

    public string? Attributes { get; init; }

    public string Status { get; init; }

    /// <summary>Derived via FamilyId -> Category -> MaterialDomain (D-CAT-01) - no direct FK exists.</summary>
    public Guid MaterialDomainId { get; init; }

    public string MaterialDomainName { get; init; }

    /// <summary>The material's own base unit (<c>Material.BaseUnitId</c>, decision 038).</summary>
    public Guid BaseUnitId { get; init; }

    public string BaseUnitSymbol { get; init; }
}
