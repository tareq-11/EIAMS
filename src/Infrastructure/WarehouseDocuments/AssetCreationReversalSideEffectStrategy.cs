using Application.Abstractions.Assets;
using Application.Abstractions.Data;
using Application.Abstractions.Posting;
using Domain.Assets;
using Domain.Common;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.WarehouseDocuments;

internal sealed class AssetCreationReversalSideEffectStrategy(
    IApplicationDbContext context,
    IAssetUsageChecker assetUsageChecker)
    : IDocumentReversalSideEffectStrategy
{
    private static readonly DocumentType[] SupportedTypes =
    [
        DocumentType.Receiving,
        DocumentType.Opening
    ];

    public IReadOnlyCollection<DocumentType> DocumentTypes => SupportedTypes;

    public async Task<Result> ValidateAsync(
        WarehouseDocument source,
        WarehouseDocument reversal,
        CancellationToken cancellationToken)
    {
        List<Asset> assets = await LoadSourceAssetsAsync(source.Id, cancellationToken);

        return await assetUsageChecker.HasDownstreamUsageAsync(
                assets,
                source,
                reversal.Id,
                cancellationToken)
            ? Result.Failure(AssetErrors.ReversalBlocked(source.Id))
            : Result.Success();
    }

    public async Task<Result> ApplyAsync(
        WarehouseDocument source,
        WarehouseDocument reversal,
        Guid postedBy,
        DateTime postedAtUtc,
        CancellationToken cancellationToken)
    {
        Result validationResult = await ValidateAsync(source, reversal, cancellationToken);

        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        List<Asset> assets = await LoadSourceAssetsAsync(source.Id, cancellationToken);
        context.Assets.RemoveRange(assets);

        return Result.Success();
    }

    private async Task<List<Asset>> LoadSourceAssetsAsync(
        Guid sourceDocumentId,
        CancellationToken cancellationToken)
    {
        Guid[] sourceLineIds = await context.DocumentLines
            .Where(line => line.DocumentId == sourceDocumentId)
            .Select(line => line.Id)
            .ToArrayAsync(cancellationToken);

        return await context.Assets
            .Where(asset => asset.ReceiptLineId != null && sourceLineIds.Contains(asset.ReceiptLineId.Value))
            .ToListAsync(cancellationToken);
    }
}
