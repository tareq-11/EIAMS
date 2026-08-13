using Domain.Common;
using SharedKernel;

namespace Domain.WarehouseDocuments;

public sealed record WarehouseDocumentCreatedDomainEvent(
    Guid DocumentId,
    Guid WarehouseId,
    DocumentType DocumentType,
    Guid? ReversalOfDocumentId) : IDomainEvent;
