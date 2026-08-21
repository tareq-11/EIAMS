using SharedKernel;

namespace Domain.DocumentLineAssetSelections;

public sealed class DocumentLineAssetSelection : Entity, IAuditableEntity
{
    private DocumentLineAssetSelection() { }

    public Guid DocumentId { get; private set; }
    public Guid DocumentLineId { get; private set; }
    public Guid AssetId { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Result<DocumentLineAssetSelection> Create(
        Guid id,
        Guid documentId,
        Guid documentLineId,
        Guid assetId)
    {
        if (id == Guid.Empty || documentId == Guid.Empty || documentLineId == Guid.Empty || assetId == Guid.Empty)
        {
            return Result.Failure<DocumentLineAssetSelection>(DocumentLineAssetSelectionErrors.IdentityRequired);
        }

        var selection = new DocumentLineAssetSelection
        {
            Id = id,
            DocumentId = documentId,
            DocumentLineId = documentLineId,
            AssetId = assetId
        };

        selection.Raise(new DocumentLineAssetSelectedDomainEvent(id, documentId, documentLineId, assetId));

        return selection;
    }

    public void RaiseRemovedEvent() => Raise(new DocumentLineAssetSelectionRemovedDomainEvent(
        Id,
        DocumentId,
        DocumentLineId,
        AssetId));
}
