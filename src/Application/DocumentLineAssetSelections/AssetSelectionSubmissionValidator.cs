using Application.Abstractions.Data;
using Domain.Common;
using Domain.DocumentLineAssetSelections;
using Domain.DocumentLines;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.DocumentLineAssetSelections;

/// <summary>Validates the persisted selection shape before a document can be submitted or posted.</summary>
internal static class AssetSelectionSubmissionValidator
{
    public static async Task<Result> ValidateAsync(
        IApplicationDbContext context,
        Guid documentId,
        IReadOnlyList<DocumentLine> lines,
        CancellationToken cancellationToken)
    {
        List<DocumentLineAssetSelection> selections = await context.DocumentLineAssetSelections
            .AsNoTracking()
            .Where(selection => selection.DocumentId == documentId)
            .ToListAsync(cancellationToken);
        var lineById = lines.ToDictionary(line => line.Id);

        foreach (DocumentLineAssetSelection selection in selections)
        {
            if (!lineById.TryGetValue(selection.DocumentLineId, out DocumentLine? line) ||
                line.LineType != DocumentLineType.Asset)
            {
                return Result.Failure(DocumentLineAssetSelectionErrors.UnsupportedLineType(selection.DocumentLineId));
            }
        }

        foreach (DocumentLine assetLine in lines.Where(line => line.LineType == DocumentLineType.Asset))
        {
            int selectedCount = selections.Count(selection => selection.DocumentLineId == assetLine.Id);

            if (decimal.Truncate(assetLine.BaseQuantity) != assetLine.BaseQuantity ||
                selectedCount != decimal.ToInt32(assetLine.BaseQuantity))
            {
                return Result.Failure(DocumentLineAssetSelectionErrors.CountMismatch(
                    assetLine.Id,
                    assetLine.BaseQuantity,
                    selectedCount));
            }
        }

        return Result.Success();
    }
}
