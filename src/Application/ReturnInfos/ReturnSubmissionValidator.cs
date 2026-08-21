using Application.Abstractions.Data;
using Application.Abstractions.Posting;
using Application.DocumentLineAssetSelections;
using Domain.Common;
using Domain.DocumentLines;
using Domain.ReturnInfos;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.ReturnInfos;

internal sealed class ReturnSubmissionValidator(IApplicationDbContext context) : IDocumentSubmissionValidator
{
    public DocumentType DocumentType => DocumentType.Return;

    public async Task<Result> ValidateAsync(
        WarehouseDocument document,
        IReadOnlyList<DocumentLine> lines,
        CancellationToken cancellationToken)
    {
        if (document.ReversalOfDocumentId is not null)
        {
            return Result.Success();
        }

        ReturnInfo? returnInfo = await context.ReturnInfos
            .AsNoTracking()
            .SingleOrDefaultAsync(info => info.Id == document.Id, cancellationToken);

        if (returnInfo is null)
        {
            return Result.Failure(ReturnInfoErrors.Required(document.Id));
        }

        WarehouseDocument? originalIssue = await context.WarehouseDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == returnInfo.OriginalIssueDocumentId, cancellationToken);

        if (originalIssue is null ||
            originalIssue.DocumentType != DocumentType.Issue ||
            originalIssue.DocumentStatus != DocumentStatus.Posted)
        {
            return Result.Failure(ReturnInfoErrors.OriginalIssueInvalid(returnInfo.OriginalIssueDocumentId));
        }

        if (originalIssue.WarehouseId != document.WarehouseId)
        {
            return Result.Failure(ReturnInfoErrors.WrongWarehouse(document.Id, document.WarehouseId));
        }

        return await AssetSelectionSubmissionValidator.ValidateAsync(
            context,
            document.Id,
            lines,
            cancellationToken);
    }
}
