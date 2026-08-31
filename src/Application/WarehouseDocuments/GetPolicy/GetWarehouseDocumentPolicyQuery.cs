using Application.Abstractions.Messaging;

namespace Application.WarehouseDocuments.GetPolicy;

public sealed record GetWarehouseDocumentPolicyQuery(Guid DocumentId)
    : IQuery<WarehouseDocumentPolicyResponse>;
