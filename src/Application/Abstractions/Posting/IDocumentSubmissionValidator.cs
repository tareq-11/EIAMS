using Domain.Common;
using Domain.DocumentLines;
using Domain.WarehouseDocuments;
using SharedKernel;

namespace Application.Abstractions.Posting;

/// <summary>Validates type-specific details immediately before a Draft document is submitted.</summary>
public interface IDocumentSubmissionValidator
{
    DocumentType DocumentType { get; }

    Task<Result> ValidateAsync(
        WarehouseDocument document,
        IReadOnlyList<DocumentLine> lines,
        CancellationToken cancellationToken);
}
