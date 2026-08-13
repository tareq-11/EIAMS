namespace Application.WarehouseDocuments.GetById;

public sealed class WarehouseDocumentDetailsResponse
{
    public Guid Id { get; init; }
    public Guid WarehouseId { get; init; }
    public string DocumentType { get; init; }
    public string? PaperDocumentNumber { get; init; }
    public int? PaperDocumentYear { get; init; }
    public string SystemReferenceNumber { get; init; }
    public Guid? SignedCopyAttachmentId { get; init; }
    public string DocumentStatus { get; init; }
    public Guid? PostedBy { get; init; }
    public DateTime? PostedAtUtc { get; init; }
    public Guid? ReversalOfDocumentId { get; init; }
    public Guid? ReversedByDocumentId { get; set; }
    public int RowVersion { get; init; }
    public List<DocumentLineResponse> Lines { get; set; } = [];
    public List<DocumentAttachmentResponse> Attachments { get; set; } = [];
}

public sealed class DocumentLineResponse
{
    public Guid Id { get; init; }
    public Guid? SourceLineId { get; init; }
    public Guid MaterialId { get; init; }
    public string LineType { get; init; }
    public decimal Quantity { get; init; }
    public Guid? UnitId { get; init; }
    public decimal BaseQuantity { get; init; }
    public decimal? UnitPrice { get; init; }
    public string? BatchNumber { get; init; }
    public DateOnly? ExpiryDate { get; init; }
}

public sealed class DocumentAttachmentResponse
{
    public Guid Id { get; init; }
    public string AttachmentType { get; init; }
    public string OriginalFilename { get; init; }
    public string MimeType { get; init; }
    public long FileSize { get; init; }
    public DateTime UploadedAtUtc { get; init; }
}
