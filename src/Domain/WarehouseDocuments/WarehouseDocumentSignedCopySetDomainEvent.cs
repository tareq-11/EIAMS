using SharedKernel;

namespace Domain.WarehouseDocuments;

public sealed record WarehouseDocumentSignedCopySetDomainEvent(Guid DocumentId, Guid AttachmentId) : IDomainEvent;
