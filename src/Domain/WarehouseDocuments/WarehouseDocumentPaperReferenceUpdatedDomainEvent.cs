using SharedKernel;

namespace Domain.WarehouseDocuments;

public sealed record WarehouseDocumentPaperReferenceUpdatedDomainEvent(Guid DocumentId) : IDomainEvent;
