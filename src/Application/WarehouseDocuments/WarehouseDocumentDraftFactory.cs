using Application.Abstractions.Data;
using Application.Abstractions.Numbering;
using Application.Abstractions.WarehouseDocuments;
using Domain.Common;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseDocuments;

internal sealed class WarehouseDocumentDraftFactory(
    IApplicationDbContext context,
    IReferenceNumberGenerator referenceNumberGenerator) : IWarehouseDocumentDraftFactory
{
    public async Task<Result<WarehouseDocument>> CreateAsync(
        Guid warehouseId,
        DocumentType documentType,
        CancellationToken cancellationToken)
    {
        Warehouse? warehouse = await context.Warehouses.SingleOrDefaultAsync(
            item => item.Id == warehouseId, cancellationToken);
        if (warehouse is null)
        {
            return Result.Failure<WarehouseDocument>(WarehouseErrors.NotFound(warehouseId));
        }

        if (warehouse.Status != Status.Active)
        {
            return Result.Failure<WarehouseDocument>(WarehouseErrors.Inactive(warehouseId));
        }

        if (!warehouse.CanHoldStock)
        {
            return Result.Failure<WarehouseDocument>(WarehouseErrors.CannotHoldStock(warehouseId));
        }

        Result<string> reference = await referenceNumberGenerator.AllocateAsync(
            warehouse.SiteId, documentType, cancellationToken);
        return reference.IsFailure
            ? Result.Failure<WarehouseDocument>(reference.Error)
            : WarehouseDocument.CreateDraft(Guid.NewGuid(), warehouseId, documentType, reference.Value);
    }
}
