using SharedKernel;

namespace Domain.Materials;

/// <summary>
/// A material, defined once centrally. Domain is derived via FamilyId -> Category -> MaterialDomain
/// (D-CAT-01) - there is deliberately no direct MaterialDomainId on this entity.
/// BaseUnitId directly specifies the stock and inventory base unit of measure for this material.
/// </summary>
public sealed class Material : Entity, IAuditableEntity
{
    private Material() { }

    public Guid FamilyId { get; private set; }
    public Guid BaseUnitId { get; private set; }
    public string NameAr { get; private set; }
    public string? NameEn { get; private set; }
    public string Code { get; private set; }
    public MaterialKind MaterialKind { get; private set; }
    public TrackingType TrackingType { get; private set; }
    public bool HasExpiry { get; private set; }
    public string? Attributes { get; private set; }
    public MaterialStatus Status { get; private set; }

    public int CatalogVersion { get; private set; }

    public bool RequiresAssetNumber => MaterialKind == MaterialKind.Asset;

    public bool IsAssetTracked => RequiresAssetNumber;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Material Create(
        Guid id,
        Guid familyId,
        Guid baseUnitId,
        string nameAr,
        string? nameEn,
        string code,
        MaterialKind materialKind,
        TrackingType trackingType,
        bool hasExpiry,
        string? attributes)
    {
        var material = new Material
        {
            Id = id,
            FamilyId = familyId,
            BaseUnitId = baseUnitId,
            NameAr = nameAr,
            NameEn = nameEn,
            Code = code,
            MaterialKind = materialKind,
            TrackingType = trackingType,
            HasExpiry = hasExpiry,
            Attributes = attributes,
            Status = MaterialStatus.Active,
            CatalogVersion = 1
        };

        material.Raise(new MaterialCreatedDomainEvent(material.Id, material.FamilyId));

        return material;
    }

    public void UpdateDetails(
        string nameAr,
        string? nameEn,
        MaterialKind materialKind,
        TrackingType trackingType,
        bool hasExpiry,
        string? attributes)
    {
        bool classificationChanged = MaterialKind != materialKind || TrackingType != trackingType;

        NameAr = nameAr;
        NameEn = nameEn;
        MaterialKind = materialKind;
        TrackingType = trackingType;
        HasExpiry = hasExpiry;
        Attributes = attributes;

        if (classificationChanged)
        {
            CatalogVersion++;
        }

        Raise(new MaterialUpdatedDomainEvent(Id));
    }

    public Result SetStatus(MaterialStatus status)
    {
        if (Status == status)
        {
            return Result.Success();
        }

        if (Status == MaterialStatus.Archived)
        {
            return Result.Failure(MaterialErrors.ArchivedIsTerminal);
        }

        Status = status;

        Raise(new MaterialStatusChangedDomainEvent(Id, status));

        return Result.Success();
    }
}
