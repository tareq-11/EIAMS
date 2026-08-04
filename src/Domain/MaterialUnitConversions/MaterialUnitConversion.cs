using SharedKernel;

namespace Domain.MaterialUnitConversions;

public sealed class MaterialUnitConversion : Entity, IAuditableEntity
{
    private MaterialUnitConversion() { }

    public Guid MaterialId { get; private set; }
    public Guid FromUnitId { get; private set; }
    public Guid ToBaseUnitId { get; private set; }
    public decimal Factor { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static MaterialUnitConversion Create(
        Guid id,
        Guid materialId,
        Guid fromUnitId,
        Guid toBaseUnitId,
        decimal factor)
    {
        var conversion = new MaterialUnitConversion
        {
            Id = id,
            MaterialId = materialId,
            FromUnitId = fromUnitId,
            ToBaseUnitId = toBaseUnitId,
            Factor = factor
        };

        conversion.Raise(new MaterialUnitConversionCreatedDomainEvent(conversion.Id, conversion.MaterialId));

        return conversion;
    }

    public void MarkAsRemoved()
    {
        Raise(new MaterialUnitConversionRemovedDomainEvent(Id, MaterialId));
    }
}
