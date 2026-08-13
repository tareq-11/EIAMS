using SharedKernel;

namespace Domain.WarehouseDocuments;

public sealed record WarehouseDocumentSubmittedDomainEvent(Guid DocumentId) : IDomainEvent;
