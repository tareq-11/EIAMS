using Domain.Common;
using SharedKernel;

namespace Domain.WarehouseDocuments;

public sealed record WarehouseDocumentPostedDomainEvent(Guid DocumentId, Guid WarehouseId, DocumentType DocumentType)
    : IDomainEvent;
