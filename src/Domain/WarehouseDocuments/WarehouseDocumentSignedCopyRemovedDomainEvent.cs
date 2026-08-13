using SharedKernel;

namespace Domain.WarehouseDocuments;

public sealed record WarehouseDocumentSignedCopyRemovedDomainEvent(Guid DocumentId) : IDomainEvent;
