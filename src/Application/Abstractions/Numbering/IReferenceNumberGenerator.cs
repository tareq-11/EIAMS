using Domain.Common;
using SharedKernel;

namespace Application.Abstractions.Numbering;

/// <summary>
/// Allocates the next system reference number for a (Site, DocumentType, Year) key (D-SEQ-01).
/// Implemented in Infrastructure via an atomic <c>INSERT ... ON CONFLICT ... DO UPDATE</c> upsert on
/// DocumentSequence, so concurrent callers never receive the same number. Not exposed over HTTP -
/// consumed internally by M3's document-creation workflow.
/// </summary>
public interface IReferenceNumberGenerator
{
    Task<Result<string>> AllocateAsync(Guid siteId, DocumentType documentType, CancellationToken cancellationToken);

    Task<Result<AllocatedDocumentReference>> AllocateIdentityAsync(
        Guid siteId,
        DocumentType documentType,
        CancellationToken cancellationToken);
}

/// <summary>Immutable identity of a newly allocated system document reference.</summary>
public sealed record AllocatedDocumentReference(
    Guid SiteId,
    int Year,
    int Sequence,
    string ReferenceNumber);
