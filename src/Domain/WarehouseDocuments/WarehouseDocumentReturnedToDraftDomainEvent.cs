using SharedKernel;

namespace Domain.WarehouseDocuments;

public sealed record WarehouseDocumentReturnedToDraftDomainEvent(Guid DocumentId) : IDomainEvent;
