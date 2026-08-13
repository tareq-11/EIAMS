using Application.Abstractions.Data;
using Application.Abstractions.Ledger;
using Application.Abstractions.Posting;
using Application.Abstractions.Warehouses;
using Domain.Common;
using Domain.DocumentLines;
using Domain.TransferInfos;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.WarehouseDocuments;

internal sealed class TransferPostingStrategy(
    IApplicationDbContext dbContext,
    ICapabilityCheckService capabilityCheckService) : IDocumentPostingStrategy
{
    public DocumentType DocumentType => DocumentType.Transfer;

    public async Task<Result<PostingPlan>> PrepareAsync(
        DocumentPostingContext context,
        CancellationToken cancellationToken)
    {
        if (context.Lines.Count == 0)
        {
            return Result.Failure<PostingPlan>(WarehouseDocumentErrors.LinesRequired(context.Document.Id));
        }

        if (context.Lines.Any(line => line.LineType == DocumentLineType.Asset))
        {
            return Result.Failure<PostingPlan>(TransferInfoErrors.AssetLinesNotSupported(context.Document.Id));
        }

        TransferInfo? transferInfo = await dbContext.TransferInfos
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == context.Document.Id, cancellationToken);

        if (transferInfo is null)
        {
            return Result.Failure<PostingPlan>(TransferInfoErrors.Required(context.Document.Id));
        }

        Result destinationResult = await ValidateDestinationAsync(context, transferInfo, cancellationToken);

        if (destinationResult.IsFailure)
        {
            return Result.Failure<PostingPlan>(destinationResult.Error);
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

        foreach (Guid materialDomainId in catalogResult.Value.Values
                     .Select(item => item.MaterialDomainId)
                     .Distinct())
        {
            Result sourceCapabilityResult = await capabilityCheckService.EnsureAllowedAsync(
                context.Document.WarehouseId,
                materialDomainId,
                OperationType.Transfer,
                cancellationToken);

            if (sourceCapabilityResult.IsFailure)
            {
                return Result.Failure<PostingPlan>(sourceCapabilityResult.Error);
            }

            Result destinationCapabilityResult = await capabilityCheckService.EnsureAllowedAsync(
                transferInfo.DestinationWarehouseId,
                materialDomainId,
                OperationType.Transfer,
                cancellationToken);

            if (destinationCapabilityResult.IsFailure)
            {
                return Result.Failure<PostingPlan>(destinationCapabilityResult.Error);
            }
        }

        var movements = new List<MovementDraft>(context.Lines.Count * 2);

        foreach (DocumentLine line in context.Lines)
        {
            movements.Add(new MovementDraft(
                context.Document.WarehouseId,
                line.MaterialId,
                context.Document.Id,
                line.Id,
                MovementType.TransferOut,
                -line.BaseQuantity));
            movements.Add(new MovementDraft(
                transferInfo.DestinationWarehouseId,
                line.MaterialId,
                context.Document.Id,
                line.Id,
                MovementType.TransferIn,
                line.BaseQuantity));
        }

        return new PostingPlan(movements);
    }

    public Task<Result> ApplySideEffectsAsync(
        DocumentPostingContext context,
        PostingPlan plan,
        CancellationToken cancellationToken) => Task.FromResult(Result.Success());

    private async Task<Result> ValidateDestinationAsync(
        DocumentPostingContext context,
        TransferInfo transferInfo,
        CancellationToken cancellationToken)
    {
        if (transferInfo.DestinationWarehouseId == context.Document.WarehouseId)
        {
            return Result.Failure(TransferInfoErrors.DestinationSameAsSource(
                context.Document.Id,
                context.Document.WarehouseId));
        }

        Warehouse? destination = await dbContext.Warehouses
            .AsNoTracking()
            .SingleOrDefaultAsync(warehouse => warehouse.Id == transferInfo.DestinationWarehouseId, cancellationToken);

        if (destination is null)
        {
            return Result.Failure(WarehouseErrors.NotFound(transferInfo.DestinationWarehouseId));
        }

        if (destination.Status != Status.Active)
        {
            return Result.Failure(WarehouseErrors.Inactive(destination.Id));
        }

        return destination.CanHoldStock
            ? Result.Success()
            : Result.Failure(WarehouseErrors.CannotHoldStock(destination.Id));
    }
}
