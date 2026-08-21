using SharedKernel;

namespace Domain.InventoryCounts;

public sealed class InventoryCountScopeMaterial : Entity, IAuditableEntity
{
    private InventoryCountScopeMaterial() { }

    public Guid CountId { get; private set; }
    public Guid MaterialId { get; private set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static InventoryCountScopeMaterial Create(Guid id, Guid countId, Guid materialId) => new()
    {
        Id = id,
        CountId = countId,
        MaterialId = materialId
    };
}
