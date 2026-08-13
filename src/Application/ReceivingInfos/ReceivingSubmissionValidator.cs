using Application.Abstractions.Data;
using Application.Abstractions.Posting;
using Domain.Common;
using Domain.DocumentLines;
using Domain.ReceivingInfos;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.ReceivingInfos;

internal sealed class ReceivingSubmissionValidator(IApplicationDbContext context)
    : IDocumentSubmissionValidator
{
    public DocumentType DocumentType => DocumentType.Receiving;

    public async Task<Result> ValidateAsync(
        WarehouseDocument document,
        IReadOnlyList<DocumentLine> lines,
        CancellationToken cancellationToken)
    {
        bool hasReceivingInfo = await context.ReceivingInfos
            .AsNoTracking()
            .AnyAsync(info => info.Id == document.Id, cancellationToken);

        return hasReceivingInfo
            ? Result.Success()
            : Result.Failure(ReceivingInfoErrors.Required(document.Id));
    }
}
