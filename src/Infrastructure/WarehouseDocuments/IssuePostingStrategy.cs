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
using Domain.IssueTos;
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

        foreach (Guid materialDomainId in catalogResult.Value.Values
                     .Select(item => item.MaterialDomainId)
                     .Distinct())
        {
            Result capabilityResult = await capabilityCheckService.EnsureAllowedAsync(
                context.Document.WarehouseId,
                materialDomainId,
                OperationType.Issue,
                cancellationToken);

            if (capabilityResult.IsFailure)
            {
                return Result.Failure<PostingPlan>(capabilityResult.Error);
            }
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

        return new PostingPlan(context.Lines
            .Select(line => new MovementDraft(
                context.Document.WarehouseId,
                line.MaterialId,
                context.Document.Id,
                line.Id,
                MovementType.Issue,
                -line.BaseQuantity))
            .ToList());
    }

    public async Task<Result> ApplySideEffectsAsync(
        DocumentPostingContext context,
        PostingPlan plan,
        CancellationToken cancellationToken)
    {
        var assetLines = context.Lines
            .Where(line => line.LineType == DocumentLineType.Asset)
            .ToList();

        if (assetLines.Count == 0)
        {
            return Result.Success();
        }

        IssueTo? issueTo = await dbContext.IssueTos
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == context.Document.Id, cancellationToken);

        if (issueTo is null)
        {
            return Result.Failure(IssueToErrors.Required(context.Document.Id));
        }

        CustodyKind custodyKind = issueTo.RecipientType == PartyType.Employee
            ? CustodyKind.Personal
            : CustodyKind.Operational;

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
                return Result.Failure(historyResult.Error);
            }

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
                return Result.Failure(custodyResult.Error);
            }

            dbContext.AssetMovementHistories.Add(historyResult.Value);
            dbContext.Custodies.Add(custodyResult.Value);
        }

        return Result.Success();
    }

    private async Task<Result> EnsureRecipientActiveAsync(IssueTo issueTo, CancellationToken cancellationToken)
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
            _ => Result.Failure(IssueToErrors.ExternalRecipientNotSupported)
        };
    }
}
