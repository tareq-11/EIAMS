using SharedKernel;
using Domain.Common;

namespace Domain.UnitsOfMeasure;

public sealed class UnitOfMeasure : Entity, IAuditableEntity
{
    private UnitOfMeasure() { }

    public string Name { get; private set; }
    public string Symbol { get; private set; }
    public string UnitType { get; private set; }
    public Status Status { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static UnitOfMeasure Create(Guid id, string name, string symbol, string unitType)
    {
        var unit = new UnitOfMeasure
        {
            Id = id,
            Name = name,
            Symbol = symbol,
            UnitType = unitType,
            Status = Status.Active
        };

        unit.Raise(new UnitOfMeasureCreatedDomainEvent(unit.Id));

        return unit;
    }

    public void SetStatus(Status status)
    {
        if (Status == status)
        {
            return;
        }
        Status = status;
        Raise(new UnitOfMeasureUpdatedDomainEvent(Id));
    }

    public void UpdateDetails(string name, string symbol, string unitType)
    {
        Name = name;
        Symbol = symbol;
        UnitType = unitType;
        Raise(new UnitOfMeasureUpdatedDomainEvent(Id));
    }
}
