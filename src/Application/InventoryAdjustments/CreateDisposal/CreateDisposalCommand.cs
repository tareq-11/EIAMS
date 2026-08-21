using Application.Abstractions.Messaging;

namespace Application.InventoryAdjustments.CreateDisposal;

public sealed record CreateDisposalCommand(
    Guid WarehouseId,
    IReadOnlyCollection<Guid> AssetIds,
    string Reason) : ICommand<Guid>
{
    public CreateDisposalCommand(Guid warehouseId, Guid assetId, string reason)
        : this(warehouseId, [assetId], reason)
    {
    }
}
