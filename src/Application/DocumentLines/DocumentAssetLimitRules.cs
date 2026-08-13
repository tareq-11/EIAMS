using Application.Abstractions.Assets;
using Domain.DocumentLines;
using SharedKernel;

namespace Application.DocumentLines;

public static class DocumentAssetLimitRules
{
    public static Result Validate(
        Guid documentId,
        int lineCount,
        decimal totalAssetBaseQuantity,
        AssetCreationOptions options)
    {
        if (lineCount > options.MaxLinesPerDocument)
        {
            return Result.Failure(DocumentLineErrors.LinesLimitExceeded(
                documentId,
                lineCount,
                options.MaxLinesPerDocument));
        }

        if (totalAssetBaseQuantity > options.MaxAssetsPerDocument)
        {
            return Result.Failure(DocumentLineErrors.AssetDocumentLimitExceeded(
                documentId,
                totalAssetBaseQuantity,
                options.MaxAssetsPerDocument));
        }

        return Result.Success();
    }
}
