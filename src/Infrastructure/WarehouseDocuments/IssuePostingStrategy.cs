using Application.Abstractions.Data;
using Application.Abstractions.Ledger;
using Application.Abstractions.Posting;
using Application.Abstractions.Recipients;
using Application.Abstractions.Warehouses;
using Domain.AssetMovementHistories;
using Domain.Common;
using Domain.Custodies;
using Domain.DocumentLineAssetSelections;
using Domain.DocumentLines;
using Domain.DurableCustodies;
using Domain.DurableCustodyAllocations;
using Domain.IssueTos;
using Domain.Materials;
using Domain.TrackedMaterialUnits;
using Domain.WarehouseDocuments;
using Infrastructure.Assets;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.WarehouseDocuments;

internal sealed class IssuePostingStrategy(
    IApplicationDbContext dbContext,
    ICapabilityCheckService capabilityCheckService,
    IActivePartyLookup activePartyLookup,
    AssetPostingSelectionService assetPostingSelectionService) : IDocumentPostingStrategy
{
    private IssueTo? preparedIssueTo;

    public DocumentType DocumentType => DocumentType.Issue;

    public async Task<Result<PostingPlan>> PrepareAsync(
        DocumentPostingContext context,
        CancellationToken cancellationToken)
    {
        if (context.Lines.Count == 0)
        {
            return Result.Failure<PostingPlan>(WarehouseDocumentErrors.LinesRequired(context.Document.Id));
        }

        IssueTo? issueTo = await dbContext.IssueTos
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == context.Document.Id, cancellationToken);

        if (issueTo is null)
        {
            return Result.Failure<PostingPlan>(IssueToErrors.Required(context.Document.Id));
        }

        preparedIssueTo = issueTo;

        Result recipientResult = await EnsureRecipientActiveAsync(issueTo, cancellationToken);

        if (recipientResult.IsFailure)
        {
            return Result.Failure<PostingPlan>(recipientResult.Error);
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

        Result capabilityResult = await capabilityCheckService.EnsureAllowedBatchAsync(
            context.Document.WarehouseId,
            materialDomainIds,
            OperationType.Issue,
            cancellationToken);

        if (capabilityResult.IsFailure)
        {
            return Result.Failure<PostingPlan>(capabilityResult.Error);
        }

        Result<IReadOnlyList<Domain.Assets.Asset>> assetSelectionsResult =
            await assetPostingSelectionService.LockAndValidateForIssueAsync(
                context.Document,
                context.Lines,
                cancellationToken);

        if (assetSelectionsResult.IsFailure)
        {
            return Result.Failure<PostingPlan>(assetSelectionsResult.Error);
        }

        var movements = context.Lines
            .Select(line => new MovementDraft(
                context.Document.WarehouseId,
                line.MaterialId,
                context.Document.Id,
                line.Id,
                MovementType.Issue,
                -line.BaseQuantity))
            .ToList();

        return new PostingPlan(movements);
    }

    public async Task<Result> ApplySideEffectsAsync(
        DocumentPostingContext context,
        PostingPlan plan,
        CancellationToken cancellationToken)
    {
        IssueTo? issueTo = preparedIssueTo ?? await dbContext.IssueTos
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == context.Document.Id, cancellationToken);

        if (issueTo is null)
        {
            return Result.Failure(IssueToErrors.Required(context.Document.Id));
        }

        CustodyKind custodyKind = issueTo.RecipientType == PartyType.Employee
            ? CustodyKind.Personal
            : CustodyKind.Operational;

        // 1. Asset Lines
        var assetLines = context.Lines
            .Where(line => line.LineType == DocumentLineType.Asset)
            .ToList();

        if (assetLines.Count > 0)
        {
            foreach (Domain.Assets.Asset asset in assetPostingSelectionService
                         .GetPreparedIssueSelections(context.Document.Id))
            {
                Result<AssetMovementHistory> historyResult = AssetMovementHistory.Create(
                    Guid.NewGuid(),
                    asset.Id,
                    context.Document.Id,
                    AssetMovementType.Issued,
                    context.PostedAtUtc);

                if (historyResult.IsFailure)
                {
                    return historyResult;
                }

                dbContext.AssetMovementHistories.Add(historyResult.Value);

                Result<Custody> custodyResult = Custody.Open(
                    Guid.NewGuid(),
                    asset.Id,
                    issueTo.RecipientType,
                    issueTo.RecipientId,
                    custodyKind,
                    context.Document.Id,
                    context.PostedAtUtc);

                if (custodyResult.IsFailure)
                {
                    return custodyResult;
                }

                dbContext.Custodies.Add(custodyResult.Value);
            }
        }

        // 2. Durable Lines
        Guid[] materialIds = context.Lines.Select(l => l.MaterialId).Distinct().ToArray();
        List<Material> materials = await dbContext.Materials
            .AsNoTracking()
            .Where(m => materialIds.Contains(m.Id))
            .ToListAsync(cancellationToken);

        var materialById = materials.ToDictionary(m => m.Id);

        foreach (DocumentLine line in context.Lines)
        {
            if (!materialById.TryGetValue(line.MaterialId, out Material? material))
            {
                continue;
            }

            if (material.MaterialKind != MaterialKind.Durable)
            {
                continue;
            }

            if (material.TrackingType == TrackingType.Quantity)
            {
                Result<DurableCustodyAllocation> allocResult = DurableCustodyAllocation.Open(
                    Guid.NewGuid(),
                    line.MaterialId,
                    context.Document.WarehouseId,
                    issueTo.RecipientType,
                    issueTo.RecipientId,
                    custodyKind,
                    context.Document.Id,
                    line.BaseQuantity,
                    context.PostedAtUtc);

                if (allocResult.IsFailure)
                {
                    return allocResult;
                }

                dbContext.DurableCustodyAllocations.Add(allocResult.Value);

                dbContext.DurableCustodyHistories.Add(DurableCustodyHistory.Record(
                    Guid.NewGuid(),
                    CustodySubjectType.MaterialQuantity,
                    allocResult.Value.Id,
                    "Issued",
                    null,
                    null,
                    issueTo.RecipientType,
                    issueTo.RecipientId,
                    line.BaseQuantity,
                    context.Document.Id,
                    context.PostedAtUtc,
                    context.PostedBy));
            }
            else if (material.TrackingType == TrackingType.Serial)
            {
                string serialNumber = !string.IsNullOrWhiteSpace(line.BatchNumber)
                    ? line.BatchNumber
                    : $"SN-{Guid.NewGuid():N}"[..12];

                Result<TrackedMaterialUnit> unitResult = TrackedMaterialUnit.Issue(
                    Guid.NewGuid(),
                    line.MaterialId,
                    serialNumber,
                    context.Document.WarehouseId,
                    issueTo.RecipientType,
                    issueTo.RecipientId,
                    custodyKind,
                    context.Document.Id,
                    context.PostedAtUtc);

                if (unitResult.IsFailure)
                {
                    return unitResult;
                }

                dbContext.TrackedMaterialUnits.Add(unitResult.Value);

                dbContext.DurableCustodyHistories.Add(DurableCustodyHistory.Record(
                    Guid.NewGuid(),
                    CustodySubjectType.TrackedUnit,
                    unitResult.Value.Id,
                    "Issued",
                    null,
                    null,
                    issueTo.RecipientType,
                    issueTo.RecipientId,
                    1m,
                    context.Document.Id,
                    context.PostedAtUtc,
                    context.PostedBy));
            }
        }

        return Result.Success();
    }

    private async Task<Result> EnsureRecipientActiveAsync(
        IssueTo issueTo,
        CancellationToken cancellationToken)
    {
        ActivePartyLookupStatus status = await activePartyLookup.GetStatusAsync(
            issueTo.RecipientType,
            issueTo.RecipientId,
            cancellationToken);

        return status switch
        {
            ActivePartyLookupStatus.Active => Result.Success(),
            ActivePartyLookupStatus.NotFound => Result.Failure(
                IssueToErrors.RecipientNotFound(issueTo.RecipientType, issueTo.RecipientId)),
            ActivePartyLookupStatus.Inactive => Result.Failure(
                IssueToErrors.RecipientInactive(issueTo.RecipientType, issueTo.RecipientId)),
            _ => Result.Failure(IssueToErrors.RecipientNotFound(issueTo.RecipientType, issueTo.RecipientId))
        };
    }
}
