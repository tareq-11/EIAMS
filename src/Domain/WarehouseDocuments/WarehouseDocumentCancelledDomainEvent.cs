using SharedKernel;

namespace Domain.WarehouseDocuments;

public sealed record WarehouseDocumentCancelledDomainEvent(Guid DocumentId) : IDomainEvent;
