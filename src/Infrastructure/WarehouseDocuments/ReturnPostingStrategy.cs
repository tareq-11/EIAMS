using Application.Abstractions.Data;
using Application.Abstractions.Ledger;
using Application.Abstractions.Posting;
using Application.Abstractions.Warehouses;
using Domain.AssetMovementHistories;
using Domain.Common;
using Domain.Custodies;
using Domain.CustodyHistories;
using Domain.DocumentLines;
using Domain.ReturnInfos;
using Domain.WarehouseDocuments;
using Infrastructure.Assets;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.WarehouseDocuments;

internal sealed class ReturnPostingStrategy(
    IApplicationDbContext dbContext,
    ICapabilityCheckService capabilityCheckService,
    AssetPostingSelectionService assetPostingSelectionService) : IDocumentPostingStrategy
{
    public DocumentType DocumentType => DocumentType.Return;

    public async Task<Result<PostingPlan>> PrepareAsync(
        DocumentPostingContext context,
        CancellationToken cancellationToken)
    {
        if (context.Lines.Count == 0)
        {
            return Result.Failure<PostingPlan>(WarehouseDocumentErrors.LinesRequired(context.Document.Id));
        }

        ReturnInfo? returnInfo = await dbContext.ReturnInfos
            .AsNoTracking()
            .SingleOrDefaultAsync(info => info.Id == context.Document.Id, cancellationToken);

        if (returnInfo is null)
        {
            return Result.Failure<PostingPlan>(ReturnInfoErrors.Required(context.Document.Id));
        }

        Result originalIssueResult = await ValidateOriginalIssueAsync(context.Document, returnInfo, cancellationToken);

        if (originalIssueResult.IsFailure)
        {
            return Result.Failure<PostingPlan>(originalIssueResult.Error);
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
                     .Select(material => material.MaterialDomainId)
                     .Distinct())
        {
            Result capabilityResult = await capabilityCheckService.EnsureAllowedAsync(
                context.Document.WarehouseId,
                materialDomainId,
                OperationType.Return,
                cancellationToken);

            if (capabilityResult.IsFailure)
            {
                return Result.Failure<PostingPlan>(capabilityResult.Error);
            }
        }

        Result<IReadOnlyList<AssetCustodySelection>> assetSelectionsResult =
            await assetPostingSelectionService.LockAndValidateForReturnAsync(
                context.Document,
                returnInfo,
                context.Lines,
                cancellationToken);

        if (assetSelectionsResult.IsFailure)
        {
            return Result.Failure<PostingPlan>(assetSelectionsResult.Error);
        }

        return new PostingPlan(context.Lines
            .Select(line => new MovementDraft(
                context.Document.WarehouseId,
                line.MaterialId,
                context.Document.Id,
                line.Id,
                MovementType.Receipt,
                line.BaseQuantity))
            .ToList());
    }

    public Task<Result> ApplySideEffectsAsync(
        DocumentPostingContext context,
        PostingPlan plan,
        CancellationToken cancellationToken)
    {
        foreach (AssetCustodySelection selection in assetPostingSelectionService
                     .GetPreparedReturnSelections(context.Document.Id))
        {
            Result closeResult = selection.Custody.Close(context.Document.Id, context.PostedAtUtc);

            if (closeResult.IsFailure)
            {
                return Task.FromResult(closeResult);
            }

            Result<AssetMovementHistory> movementHistoryResult = AssetMovementHistory.Create(
                Guid.NewGuid(),
                selection.Asset.Id,
                context.Document.Id,
                AssetMovementType.Returned,
                context.PostedAtUtc);

            if (movementHistoryResult.IsFailure)
            {
                return Task.FromResult(Result.Failure(movementHistoryResult.Error));
            }

            Result<CustodyHistory> custodyHistoryResult = CustodyHistory.Create(
                Guid.NewGuid(),
                selection.Custody.Id,
                CustodyStatus.Active,
                CustodyStatus.Closed,
                context.PostedBy,
                context.PostedAtUtc,
                null);

            if (custodyHistoryResult.IsFailure)
            {
                return Task.FromResult(Result.Failure(custodyHistoryResult.Error));
            }

            dbContext.AssetMovementHistories.Add(movementHistoryResult.Value);
            dbContext.CustodyHistories.Add(custodyHistoryResult.Value);
        }

        return Task.FromResult(Result.Success());
    }

    private async Task<Result> ValidateOriginalIssueAsync(
        WarehouseDocument document,
        ReturnInfo returnInfo,
        CancellationToken cancellationToken)
    {
        WarehouseDocument? originalIssue = await dbContext.WarehouseDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == returnInfo.OriginalIssueDocumentId, cancellationToken);

        if (originalIssue is null ||
            originalIssue.DocumentType != DocumentType.Issue ||
            originalIssue.DocumentStatus != DocumentStatus.Posted)
        {
            return Result.Failure(ReturnInfoErrors.OriginalIssueInvalid(returnInfo.OriginalIssueDocumentId));
        }

        return originalIssue.WarehouseId == document.WarehouseId
            ? Result.Success()
            : Result.Failure(ReturnInfoErrors.WrongWarehouse(document.Id, document.WarehouseId));
    }
}
