using Application.Abstractions.Data;
using Application.Abstractions.Posting;
using Domain.Common;
using Domain.DocumentLines;
using Domain.TransferInfos;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.TransferInfos;

internal sealed class TransferSubmissionValidator(IApplicationDbContext context) : IDocumentSubmissionValidator
{
    public DocumentType DocumentType => DocumentType.Transfer;

    public async Task<Result> ValidateAsync(
        WarehouseDocument document,
        IReadOnlyList<DocumentLine> lines,
        CancellationToken cancellationToken)
    {
        if (document.ReversalOfDocumentId is not null)
        {
            return Result.Success();
        }

        TransferInfo? transferInfo = await context.TransferInfos
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == document.Id, cancellationToken);

        if (transferInfo is null)
        {
            return Result.Failure(TransferInfoErrors.Required(document.Id));
        }

        Result destinationResult = await ValidateDestinationAsync(context, document, transferInfo, cancellationToken);

        if (destinationResult.IsFailure)
        {
            return destinationResult;
        }

        return lines.Any(line => line.LineType == DocumentLineType.Asset)
            ? Result.Failure(TransferInfoErrors.AssetLinesNotSupported(document.Id))
            : Result.Success();
    }

    internal static async Task<Result> ValidateDestinationAsync(
        IApplicationDbContext context,
        WarehouseDocument document,
        TransferInfo transferInfo,
        CancellationToken cancellationToken)
    {
        if (transferInfo.DestinationWarehouseId == document.WarehouseId)
        {
            return Result.Failure(TransferInfoErrors.DestinationSameAsSource(document.Id, document.WarehouseId));
        }

        Warehouse? destination = await context.Warehouses
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
