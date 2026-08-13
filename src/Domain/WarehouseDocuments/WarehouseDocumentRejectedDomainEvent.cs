using SharedKernel;

namespace Domain.WarehouseDocuments;

public sealed record WarehouseDocumentRejectedDomainEvent(Guid DocumentId) : IDomainEvent;
