using SharedKernel;

namespace Domain.Materials;

/// <summary>
/// A material, defined once centrally. Domain is derived via FamilyId -> Category -> MaterialDomain
/// (D-CAT-01) - there is deliberately no direct MaterialDomainId on this entity.
/// </summary>
public sealed class Material : Entity, IAuditableEntity
{
    private Material() { }

    public Guid FamilyId { get; private set; }
    public string NameAr { get; private set; }
    public string? NameEn { get; private set; }
    public string Code { get; private set; }
    public MaterialKind MaterialKind { get; private set; }
    public TrackingType TrackingType { get; private set; }
    public bool HasExpiry { get; private set; }
    public bool RequiresAssetNumber { get; private set; }
    public string? Attributes { get; private set; }
    public MaterialStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Material Create(
        Guid id,
        Guid familyId,
        string nameAr,
        string? nameEn,
        string code,
        MaterialKind materialKind,
        TrackingType trackingType,
        bool hasExpiry,
        bool requiresAssetNumber,
        string? attributes)
    {
        var material = new Material
        {
            Id = id,
            FamilyId = familyId,
            NameAr = nameAr,
            NameEn = nameEn,
            Code = code,
            MaterialKind = materialKind,
            TrackingType = trackingType,
            HasExpiry = hasExpiry,
            RequiresAssetNumber = requiresAssetNumber,
            Attributes = attributes,
            Status = MaterialStatus.Active
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
        bool requiresAssetNumber,
        string? attributes)
    {
        NameAr = nameAr;
        NameEn = nameEn;
        MaterialKind = materialKind;
        TrackingType = trackingType;
        HasExpiry = hasExpiry;
        RequiresAssetNumber = requiresAssetNumber;
        Attributes = attributes;
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
