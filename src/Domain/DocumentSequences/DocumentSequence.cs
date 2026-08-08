using Domain.Common;
using SharedKernel;

namespace Domain.DocumentSequences;

/// <summary>
/// System-managed reference-number counter, keyed by (SiteId, DocumentType, Year) with annual reset
/// (D-SEQ-01). In production, rows are created and incremented exclusively by the atomic
/// <c>INSERT ... ON CONFLICT ... DO UPDATE</c> upsert in the Infrastructure
/// <c>ReferenceNumberGenerator</c> - not through this factory or EF change tracking. <see cref="Create"/>
/// exists for tests and seeding, where materializing a row through the normal entity API is useful.
/// </summary>
public sealed class DocumentSequence : Entity, IAuditableEntity
{
    private DocumentSequence() { }

    public Guid SiteId { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public int Year { get; private set; }
    public int LastSequence { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static DocumentSequence Create(Guid id, Guid siteId, DocumentType documentType, int year, int lastSequence) =>
        new()
        {
            Id = id,
            SiteId = siteId,
            DocumentType = documentType,
            Year = year,
            LastSequence = lastSequence
        };
}
