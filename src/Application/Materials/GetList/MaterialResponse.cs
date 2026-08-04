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

    public string? Attributes { get; init; }

    public string Status { get; init; }

    /// <summary>Derived via FamilyId -> Category -> MaterialDomain (D-CAT-01) - no direct FK exists.</summary>
    public Guid MaterialDomainId { get; init; }

    public string MaterialDomainName { get; init; }

    /// <summary>Resolved via the family's base unit.</summary>
    public Guid BaseUnitId { get; init; }

    public string BaseUnitSymbol { get; init; }
}
