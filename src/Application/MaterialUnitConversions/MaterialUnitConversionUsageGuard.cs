using Application.Abstractions.Data;
using Domain.Common;
using Domain.DocumentLines;
using Domain.MaterialUnitConversions;
using Domain.StockMovements;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.MaterialUnitConversions;

/// <summary>
/// Provenance lock for a material unit conversion (3B, decision 009). Once a submitted or posted
/// document line - or a posted stock movement - depends on a conversion's factor, that factor is
/// part of an accounting fact: the conversion may no longer be retargeted, re-factored or removed.
/// Draft lines are deliberately not covered; their provenance is re-validated at submit, so a draft
/// can be corrected by editing the line (which re-captures the factor) instead of being blocked.
/// </summary>
internal static class MaterialUnitConversionUsageGuard
{
    public static async Task<Result> EnsureMutableAsync(
        IApplicationDbContext context,
        Guid conversionId,
        CancellationToken cancellationToken)
    {
        bool used = await (
                from line in context.DocumentLines.AsNoTracking()
                join document in context.WarehouseDocuments.AsNoTracking() on line.DocumentId equals document.Id
                where line.SourceConversionId == conversionId &&
                      (document.DocumentStatus == DocumentStatus.Submitted ||
                       document.DocumentStatus == DocumentStatus.Posted ||
                       document.DocumentStatus == DocumentStatus.Reversed)
                select line.Id)
            .AnyAsync(cancellationToken);

        if (used)
        {
            return Result.Failure(MaterialUnitConversionErrors.ProvenanceInUse(conversionId));
        }

        bool movementExists = await (
                from movement in context.StockMovements.AsNoTracking()
                join line in context.DocumentLines.AsNoTracking() on movement.LineId equals line.Id
                where line.SourceConversionId == conversionId
                select movement.Id)
            .AnyAsync(cancellationToken);

        return movementExists
            ? Result.Failure(MaterialUnitConversionErrors.ProvenanceInUse(conversionId))
            : Result.Success();
    }
}
