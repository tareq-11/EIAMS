using Application.Abstractions.Data;
using Application.Abstractions.Posting;
using Domain.Common;
using Domain.InventoryAdjustments;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.WarehouseDocuments;

internal sealed class AdjustmentReversalSideEffectStrategy(IApplicationDbContext dbContext)
    : IDocumentReversalSideEffectStrategy
{
    public IReadOnlyCollection<DocumentType> DocumentTypes { get; } = [DocumentType.Adjustment];

    public async Task<Result> ValidateAsync(
        WarehouseDocument source,
        WarehouseDocument reversal,
        CancellationToken cancellationToken)
    {
        InventoryAdjustment? adjustment = await dbContext.InventoryAdjustments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == source.Id, cancellationToken);
        if (adjustment is null)
        {
            return Result.Failure(InventoryAdjustmentErrors.Required(source.Id));
        }

        return adjustment.AdjustmentKind == AdjustmentKind.Disposal
            ? Result.Failure(InventoryAdjustmentErrors.DisposalReversalNotAllowed(source.Id))
            : Result.Success();
    }

    public async Task<Result> ApplyAsync(
        WarehouseDocument source,
        WarehouseDocument reversal,
        Guid postedBy,
        DateTime postedAtUtc,
        CancellationToken cancellationToken)
    {
        InventoryAdjustment? adjustment = await dbContext.InventoryAdjustments
            .SingleOrDefaultAsync(item => item.Id == source.Id, cancellationToken);
        return adjustment is null
            ? Result.Failure(InventoryAdjustmentErrors.Required(source.Id))
            : adjustment.MarkReversed();
    }
}
