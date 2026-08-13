using Application.Abstractions.Data;
using Application.Abstractions.Ledger;
using Application.Abstractions.Posting;
using Application.Abstractions.Recipients;
using Application.Abstractions.Warehouses;
using Domain.Common;
using Domain.DocumentLines;
using Domain.IssueTos;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.WarehouseDocuments;

internal sealed class IssuePostingStrategy(
    IApplicationDbContext dbContext,
    ICapabilityCheckService capabilityCheckService,
    IActivePartyLookup activePartyLookup) : IDocumentPostingStrategy
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

        if (context.Lines.Any(line => line.LineType == DocumentLineType.Asset))
        {
            return Result.Failure<PostingPlan>(IssueToErrors.AssetLinesNotSupported(context.Document.Id));
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

    public Task<Result> ApplySideEffectsAsync(
        DocumentPostingContext context,
        PostingPlan plan,
        CancellationToken cancellationToken) => Task.FromResult(Result.Success());

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
