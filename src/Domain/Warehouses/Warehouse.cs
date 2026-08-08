using Domain.Common;
using SharedKernel;

namespace Domain.Warehouses;

/// <summary>
/// A stock-holding unit within a Site. <see cref="RowVersion"/> is an optimistic-concurrency token:
/// it starts at 1 and is incremented on every successful mutation (see M2-PLAN.md §1.4).
/// </summary>
public sealed class Warehouse : Entity, IAuditableEntity
{
    private Warehouse() { }

    public Guid SiteId { get; private set; }
    public string Name { get; private set; }
    public string Code { get; private set; }
    public string WarehouseType { get; private set; }
    public bool CanHoldStock { get; private set; }
    public Status Status { get; private set; }
    public int RowVersion { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Warehouse Create(
        Guid id,
        Guid siteId,
        string name,
        string code,
        string warehouseType,
        bool canHoldStock)
    {
        var warehouse = new Warehouse
        {
            Id = id,
            SiteId = siteId,
            Name = name,
            Code = code,
            WarehouseType = warehouseType,
            CanHoldStock = canHoldStock,
            Status = Status.Active,
            RowVersion = 1
        };

        warehouse.Raise(new WarehouseCreatedDomainEvent(warehouse.Id, warehouse.SiteId));

        return warehouse;
    }

    public void UpdateDetails(string name, string warehouseType, bool canHoldStock)
    {
        Name = name;
        WarehouseType = warehouseType;
        CanHoldStock = canHoldStock;
        RowVersion++;

        Raise(new WarehouseUpdatedDomainEvent(Id));
    }

    public void SetStatus(Status status)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;
        RowVersion++;

        Raise(new WarehouseStatusChangedDomainEvent(Id, status));
    }
}
