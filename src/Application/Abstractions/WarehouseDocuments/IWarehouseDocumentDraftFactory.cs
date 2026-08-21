using Domain.Common;
using Domain.WarehouseDocuments;
using SharedKernel;

namespace Application.Abstractions.WarehouseDocuments;

public interface IWarehouseDocumentDraftFactory
{
    Task<Result<WarehouseDocument>> CreateAsync(
        Guid warehouseId,
        DocumentType documentType,
        CancellationToken cancellationToken);
}
