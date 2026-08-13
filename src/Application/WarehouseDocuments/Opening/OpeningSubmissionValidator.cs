using Application.Abstractions.Data;
using Application.Abstractions.Posting;
using Domain.Common;
using Domain.DocumentLines;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseDocuments.Opening;

internal sealed class OpeningSubmissionValidator(IApplicationDbContext context)
    : IDocumentSubmissionValidator
{
    public DocumentType DocumentType => DocumentType.Opening;

    public async Task<Result> ValidateAsync(
        WarehouseDocument document,
        IReadOnlyList<DocumentLine> lines,
        CancellationToken cancellationToken)
    {
        DocumentLine? correctionLine = lines.FirstOrDefault(line => line.OpeningType == OpeningType.Correction);

        if (correctionLine is not null)
        {
            return Result.Failure(OpeningDocumentErrors.CorrectionRequiresAdjustment(
                document.Id,
                correctionLine.Id));
        }

        Guid? duplicateMaterialId = lines
            .GroupBy(line => line.MaterialId)
            .Where(group => group.Count() > 1)
            .Select(group => (Guid?)group.Key)
            .FirstOrDefault();

        if (duplicateMaterialId is not null)
        {
            return Result.Failure(OpeningDocumentErrors.DuplicateMaterial(
                document.Id,
                duplicateMaterialId.Value));
        }

        Guid[] materialIds = lines.Select(line => line.MaterialId).ToArray();

        Guid? initializedMaterialId = await context.StockMovements
            .AsNoTracking()
            .Where(movement =>
                movement.WarehouseId == document.WarehouseId &&
                materialIds.Contains(movement.MaterialId))
            .Select(movement => (Guid?)movement.MaterialId)
            .FirstOrDefaultAsync(cancellationToken);

        return initializedMaterialId is null
            ? Result.Success()
            : Result.Failure(OpeningDocumentErrors.AlreadyInitialized(
                document.WarehouseId,
                initializedMaterialId.Value));
    }
}
