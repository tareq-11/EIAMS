using Application.Abstractions.Data;
using Domain.Common;
using Domain.InventoryAdjustments;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.InventoryAdjustments;

internal static class AdjustmentLineMutationGuard
{
    public static async Task<Result> ValidateAsync(
        IApplicationDbContext context,
        WarehouseDocument document,
        int expectedRowVersion,
        CancellationToken cancellationToken)
    {
        if (document.RowVersion != expectedRowVersion)
        {
            return Result.Failure(WarehouseDocumentErrors.RowVersionMismatch(
                document.Id, expectedRowVersion, document.RowVersion));
        }

        if (document.DocumentType != DocumentType.Adjustment)
        {
            return Result.Failure(InventoryAdjustmentErrors.WrongDocumentType(document.Id));
        }

        if (document.DocumentStatus != DocumentStatus.Draft || document.ReversalOfDocumentId is not null)
        {
            return Result.Failure(WarehouseDocumentErrors.NotEditable(document.Id, document.DocumentStatus));
        }

        var adjustment = await context.InventoryAdjustments.AsNoTracking()
            .Where(item => item.Id == document.Id)
            .Select(item => new { item.AdjustmentKind, item.CountId })
            .SingleOrDefaultAsync(cancellationToken);
        if (adjustment?.CountId is not null)
        {
            return Result.Failure(InventoryAdjustmentErrors.CountLinkedImmutable(document.Id));
        }

        return adjustment?.AdjustmentKind switch
        {
            null => Result.Failure(InventoryAdjustmentErrors.Required(document.Id)),
            AdjustmentKind.Quantity => Result.Success(),
            _ => Result.Failure(AdjustmentLineErrors.AssetQuantityAdjustmentNotSupported)
        };
    }

    public static async Task<Error> RowVersionErrorAsync(
        IApplicationDbContext context,
        Guid documentId,
        int expectedRowVersion,
        CancellationToken cancellationToken)
    {
        int? current = await context.WarehouseDocuments.AsNoTracking()
            .Where(item => item.Id == documentId)
            .Select(item => (int?)item.RowVersion)
            .SingleOrDefaultAsync(cancellationToken);
        return WarehouseDocumentErrors.RowVersionMismatch(documentId, expectedRowVersion, current);
    }
}
