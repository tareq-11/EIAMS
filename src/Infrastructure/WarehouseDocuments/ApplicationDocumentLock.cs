using Application.Abstractions.Data;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.WarehouseDocuments;

internal sealed class ApplicationDocumentLock(ApplicationDbContext dbContext) : IDocumentLock
{
    public async Task<Result<WarehouseDocument>> LockAsync(Guid documentId, CancellationToken cancellationToken)
    {
        WarehouseDocument? document = await dbContext.WarehouseDocuments
            .FromSqlInterpolated($"SELECT * FROM warehouse_documents WHERE id = {documentId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        return document is null
            ? Result.Failure<WarehouseDocument>(WarehouseDocumentErrors.NotFound(documentId))
            : document;
    }
}
