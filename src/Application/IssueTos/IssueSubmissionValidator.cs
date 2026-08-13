using Application.Abstractions.Data;
using Application.Abstractions.Posting;
using Application.Abstractions.Recipients;
using Domain.Common;
using Domain.DocumentLines;
using Domain.IssueTos;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.IssueTos;

internal sealed class IssueSubmissionValidator(
    IApplicationDbContext context,
    IActivePartyLookup activePartyLookup) : IDocumentSubmissionValidator
{
    public DocumentType DocumentType => DocumentType.Issue;

    public async Task<Result> ValidateAsync(
        WarehouseDocument document,
        IReadOnlyList<DocumentLine> lines,
        CancellationToken cancellationToken)
    {
        if (document.ReversalOfDocumentId is not null)
        {
            return Result.Success();
        }

        if (lines.Any(line => line.LineType == DocumentLineType.Asset))
        {
            return Result.Failure(IssueToErrors.AssetLinesNotSupported(document.Id));
        }

        IssueTo? issueTo = await context.IssueTos
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == document.Id, cancellationToken);

        if (issueTo is null)
        {
            return Result.Failure(IssueToErrors.Required(document.Id));
        }

        ActivePartyLookupStatus recipientStatus = await activePartyLookup.GetStatusAsync(
            issueTo.RecipientType,
            issueTo.RecipientId,
            cancellationToken);

        return recipientStatus switch
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
