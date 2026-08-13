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
    public ReceivingInfoResponse? ReceivingInfo { get; set; }
    public IssueToResponse? IssueTo { get; set; }
    public TransferInfoResponse? TransferInfo { get; set; }
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
    public string? OpeningType { get; init; }
    public List<DocumentLineAssetResponse> Assets { get; set; } = [];
}

public sealed class ReceivingInfoResponse
{
    public string SupplierRef { get; init; }
    public string? SupplierInvoiceRef { get; init; }
    public string ReceivingType { get; init; }
}

public sealed class IssueToResponse
{
    public string RecipientType { get; init; }
    public Guid RecipientId { get; init; }
    public string IssueReason { get; init; }
}

public sealed class TransferInfoResponse
{
    public Guid DestinationWarehouseId { get; init; }
    public string TransferReason { get; init; }
}

public sealed class DocumentLineAssetResponse
{
    public Guid Id { get; init; }
    public Guid? WarehouseId { get; init; }
    public string AssetNumber { get; init; }
    public string? SerialNumber { get; init; }
    public DateOnly AcquisitionDate { get; init; }
    public DateOnly? WarrantyExpiry { get; init; }
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
