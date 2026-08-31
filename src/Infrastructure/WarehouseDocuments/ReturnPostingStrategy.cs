using Application.Abstractions.Data;
using Application.Abstractions.Ledger;
using Application.Abstractions.Posting;
using Application.Abstractions.Warehouses;
using Domain.AssetMovementHistories;
using Domain.Common;
using Domain.Custodies;
using Domain.CustodyHistories;
using Domain.DocumentLines;
using Domain.DurableCustodies;
using Domain.DurableCustodyAllocations;
using Domain.Materials;
using Domain.ReturnInfos;
using Domain.TrackedMaterialUnits;
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
    private readonly Dictionary<Guid, List<DurableCustodyAllocation>> preparedAllocationsByDocumentId = [];
    private readonly Dictionary<Guid, List<TrackedMaterialUnit>> preparedTrackedUnitsByDocumentId = [];

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

        Guid[] materialDomainIds = catalogResult.Value.Values
            .Select(material => material.MaterialDomainId)
            .Distinct()
            .ToArray();

        Result capabilityResult = await capabilityCheckService.EnsureAllowedBatchAsync(
            context.Document.WarehouseId,
            materialDomainIds,
            OperationType.Return,
            cancellationToken);

        if (capabilityResult.IsFailure)
        {
            return Result.Failure<PostingPlan>(capabilityResult.Error);
        }

        // Validate Asset lines
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

        // Validate Durable lines
        var preparedAllocations = new List<DurableCustodyAllocation>();
        var preparedTrackedUnits = new List<TrackedMaterialUnit>();

        Guid originalIssueId = returnInfo.OriginalIssueDocumentId;

        foreach (DocumentLine line in context.Lines)
        {
            PostingMaterialInfo matInfo = catalogResult.Value[line.MaterialId];
            if (matInfo.Material.MaterialKind != MaterialKind.Durable)
            {
                continue;
            }

            if (matInfo.Material.TrackingType == TrackingType.Quantity)
            {
                DurableCustodyAllocation? allocation = await dbContext.DurableCustodyAllocations
                    .SingleOrDefaultAsync(
                        a => a.IssueDocumentId == originalIssueId &&
                             a.MaterialId == line.MaterialId &&
                             a.Status == DurableCustodyAllocationStatus.Active,
                        cancellationToken);

                if (allocation is null)
                {
                    return Result.Failure<PostingPlan>(
                        ReturnInfoErrors.DurableAllocationNotFound(line.MaterialId, originalIssueId));
                }

                if (line.BaseQuantity > allocation.ActiveQuantity)
                {
                    return Result.Failure<PostingPlan>(
                        ReturnInfoErrors.ReturnQuantityExceedsActive(line.BaseQuantity, allocation.ActiveQuantity));
                }

                preparedAllocations.Add(allocation);
            }
            else if (matInfo.Material.TrackingType == TrackingType.Serial)
            {
                TrackedMaterialUnit? unit = await dbContext.TrackedMaterialUnits
                    .SingleOrDefaultAsync(
                        u => u.IssueDocumentId == originalIssueId &&
                             u.MaterialId == line.MaterialId &&
                             u.Status == TrackedMaterialUnitStatus.Issued &&
                             (string.IsNullOrWhiteSpace(line.BatchNumber) || u.SerialNumber == line.BatchNumber),
                        cancellationToken);

                if (unit is null)
                {
                    return Result.Failure<PostingPlan>(
                        ReturnInfoErrors.TrackedUnitNotFound(line.MaterialId, originalIssueId));
                }

                preparedTrackedUnits.Add(unit);
            }
        }

        preparedAllocationsByDocumentId[context.Document.Id] = preparedAllocations;
        preparedTrackedUnitsByDocumentId[context.Document.Id] = preparedTrackedUnits;

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
        // 1. Asset returns
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

        // 2. Durable Quantity returns
        if (preparedAllocationsByDocumentId.TryGetValue(context.Document.Id, out List<DurableCustodyAllocation>? allocations))
        {
            foreach (DurableCustodyAllocation allocation in allocations)
            {
                DocumentLine line = context.Lines.First(l => l.MaterialId == allocation.MaterialId);
                Result returnResult = allocation.RecordReturn(line.BaseQuantity, context.Document.Id, context.PostedAtUtc);
                if (returnResult.IsFailure)
                {
                    return Task.FromResult(returnResult);
                }

                dbContext.DurableCustodyHistories.Add(DurableCustodyHistory.Record(
                    Guid.NewGuid(),
                    CustodySubjectType.MaterialQuantity,
                    allocation.Id,
                    "Returned",
                    allocation.HolderType,
                    allocation.HolderId,
                    null,
                    null,
                    line.BaseQuantity,
                    context.Document.Id,
                    context.PostedAtUtc,
                    context.PostedBy));
            }
        }

        // 3. Durable Serial returns
        if (preparedTrackedUnitsByDocumentId.TryGetValue(context.Document.Id, out List<TrackedMaterialUnit>? trackedUnits))
        {
            foreach (TrackedMaterialUnit unit in trackedUnits)
            {
                Result returnResult = unit.Return(context.Document.Id, context.PostedAtUtc);
                if (returnResult.IsFailure)
                {
                    return Task.FromResult(returnResult);
                }

                dbContext.DurableCustodyHistories.Add(DurableCustodyHistory.Record(
                    Guid.NewGuid(),
                    CustodySubjectType.TrackedUnit,
                    unit.Id,
                    "Returned",
                    unit.HolderType,
                    unit.HolderId,
                    null,
                    null,
                    1m,
                    context.Document.Id,
                    context.PostedAtUtc,
                    context.PostedBy));
            }
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
