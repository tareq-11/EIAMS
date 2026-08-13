using Domain.WarehouseDocuments;
using SharedKernel;

namespace Application.Abstractions.Posting;

/// <summary>
/// Resolves every warehouse whose inventory is affected by posting a document. Transfer documents
/// affect both their source and destination; other document types affect the source only.
/// </summary>
public interface IDocumentPostingScopeResolver
{
    Task<Result<IReadOnlyCollection<Guid>>> ResolveAsync(
        WarehouseDocument document,
        CancellationToken cancellationToken);
}
