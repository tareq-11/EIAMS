using Application.Abstractions.Data;
using Domain.Common;
using Domain.DocumentLines;
using Domain.Materials;
using Domain.StockMovements;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Materials;

/// <summary>
/// "Operational use" test for a material (3B, decisions 010/032). A material is operational once a
/// document line that references it left the draft stage or a stock movement was posted from it.
/// At that point its kind and tracking type are part of the meaning of posted facts, so they are
/// locked: only descriptive edits (names, attributes, expiry flag) remain allowed.
/// </summary>
internal static class MaterialOperationalUseGuard
{
    public static async Task<Result> EnsureClassificationMutableAsync(
        IApplicationDbContext context,
        Material material,
        MaterialKind materialKind,
        TrackingType trackingType,
        CancellationToken cancellationToken)
    {
        if (material.MaterialKind == materialKind && material.TrackingType == trackingType)
        {
            return Result.Success();
        }

        bool operationallyUsed = await HasOperationalUseAsync(context, material.Id, cancellationToken);

        return operationallyUsed
            ? Result.Failure(MaterialErrors.ClassificationLocked(material.Id))
            : Result.Success();
    }

    private static async Task<bool> HasOperationalUseAsync(
        IApplicationDbContext context,
        Guid materialId,
        CancellationToken cancellationToken)
    {
        bool usedByNonDraftDocument = await (
                from line in context.DocumentLines.AsNoTracking()
                join document in context.WarehouseDocuments.AsNoTracking() on line.DocumentId equals document.Id
                where line.MaterialId == materialId &&
                      (document.DocumentStatus == DocumentStatus.Submitted ||
                       document.DocumentStatus == DocumentStatus.Posted ||
                       document.DocumentStatus == DocumentStatus.Reversed)
                select line.Id)
            .AnyAsync(cancellationToken);

        if (usedByNonDraftDocument)
        {
            return true;
        }

        return await context.StockMovements
            .AsNoTracking()
            .AnyAsync(movement => movement.MaterialId == materialId, cancellationToken);
    }
}
