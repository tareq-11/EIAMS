namespace Application.WarehouseDocuments.GetList;

public sealed class WarehouseDocumentResponse
{
    public Guid Id { get; init; }
    public Guid WarehouseId { get; init; }
    public string DocumentType { get; init; }
    public string? PaperDocumentNumber { get; init; }
    public int? PaperDocumentYear { get; init; }
    public string SystemReferenceNumber { get; init; }
    public string DocumentStatus { get; init; }
    public DateTime? PostedAtUtc { get; init; }
    public Guid? ReversalOfDocumentId { get; init; }
    public int RowVersion { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
