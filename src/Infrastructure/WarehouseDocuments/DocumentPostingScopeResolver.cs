using Application.Abstractions.Data;
using Application.Abstractions.Posting;
using Domain.Common;
using Domain.TransferInfos;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.WarehouseDocuments;

internal sealed class DocumentPostingScopeResolver(IApplicationDbContext context)
    : IDocumentPostingScopeResolver
{
    public async Task<Result<IReadOnlyCollection<Guid>>> ResolveAsync(
        WarehouseDocument document,
        CancellationToken cancellationToken)
    {
        Guid sourceWarehouseId = document.WarehouseId;

        if (document.DocumentType != DocumentType.Transfer)
        {
            return Result.Success<IReadOnlyCollection<Guid>>([sourceWarehouseId]);
        }

        Guid transferDocumentId = document.ReversalOfDocumentId ?? document.Id;
        Guid? destinationWarehouseId = await context.TransferInfos
            .AsNoTracking()
            .Where(info => info.Id == transferDocumentId)
            .Select(info => (Guid?)info.DestinationWarehouseId)
            .SingleOrDefaultAsync(cancellationToken);

        if (destinationWarehouseId is null)
        {
            return Result.Failure<IReadOnlyCollection<Guid>>(
                TransferInfoErrors.Required(transferDocumentId));
        }

        return Result.Success<IReadOnlyCollection<Guid>>(
            [sourceWarehouseId, destinationWarehouseId.Value]);
    }
}
