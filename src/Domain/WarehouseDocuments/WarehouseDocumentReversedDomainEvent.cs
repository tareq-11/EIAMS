using SharedKernel;

namespace Domain.WarehouseDocuments;

public sealed record WarehouseDocumentReversedDomainEvent(Guid DocumentId) : IDomainEvent;
