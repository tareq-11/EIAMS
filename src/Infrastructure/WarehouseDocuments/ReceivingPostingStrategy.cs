using Application.Abstractions.Assets;
using Application.Abstractions.Data;
using Application.Abstractions.Ledger;
using Application.Abstractions.Posting;
using Application.Abstractions.Warehouses;
using Domain.Assets;
using Domain.Common;
using Domain.DocumentLines;
using Domain.ReceivingInfos;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.WarehouseDocuments;

internal sealed class ReceivingPostingStrategy(
    IApplicationDbContext dbContext,
    ICapabilityCheckService capabilityCheckService,
    IReceivedAssetFactory receivedAssetFactory) : IDocumentPostingStrategy
{
    public DocumentType DocumentType => DocumentType.Receiving;

    public async Task<Result<PostingPlan>> PrepareAsync(
        DocumentPostingContext context,
        CancellationToken cancellationToken)
    {
        if (context.Lines.Count == 0)
        {
            return Result.Failure<PostingPlan>(
                Domain.WarehouseDocuments.WarehouseDocumentErrors.LinesRequired(context.Document.Id));
        }

        bool hasReceivingInfo = await dbContext.ReceivingInfos
            .AnyAsync(info => info.Id == context.Document.Id, cancellationToken);

        if (!hasReceivingInfo)
        {
            return Result.Failure<PostingPlan>(ReceivingInfoErrors.Required(context.Document.Id));
        }

        Result<IReadOnlyDictionary<Guid, PostingMaterialInfo>> catalogResult =
            await PostingMaterialCatalogLoader.LoadAsync(
                dbContext,
                context.Document.Id,
                context.Lines,
                cancellationToken);

        if (catalogResult.IsFailure)
        {
            return Result.Failure<PostingPlan>(catalogResult.Error);
        }

        Guid[] materialDomainIds = catalogResult.Value.Values
            .Select(item => item.MaterialDomainId)
            .Distinct()
            .ToArray();

        foreach (Guid materialDomainId in materialDomainIds)
        {
            Result capabilityResult = await capabilityCheckService.EnsureAllowedAsync(
                context.Document.WarehouseId,
                materialDomainId,
                OperationType.Receiving,
                cancellationToken);

            if (capabilityResult.IsFailure)
            {
                return Result.Failure<PostingPlan>(capabilityResult.Error);
            }
        }

        var movements = context.Lines
            .Select(line => new MovementDraft(
                context.Document.WarehouseId,
                line.MaterialId,
                context.Document.Id,
                line.Id,
                MovementType.Receipt,
                line.BaseQuantity))
            .ToList();

        return new PostingPlan(movements);
    }

    public Task<Result> ApplySideEffectsAsync(
        DocumentPostingContext context,
        PostingPlan plan,
        CancellationToken cancellationToken)
    {
        foreach (DocumentLine line in context.Lines)
        {
            Result<IReadOnlyList<Asset>> assetsResult = receivedAssetFactory.CreateForLine(
                line,
                context.Document.WarehouseId,
                context.PostedAtUtc);

            if (assetsResult.IsFailure)
            {
                return Task.FromResult(Result.Failure(assetsResult.Error));
            }

            dbContext.Assets.AddRange(assetsResult.Value);
        }

        return Task.FromResult(Result.Success());
    }
}
