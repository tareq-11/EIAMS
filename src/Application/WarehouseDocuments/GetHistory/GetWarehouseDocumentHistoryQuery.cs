using Application.Abstractions.Messaging;

namespace Application.WarehouseDocuments.GetHistory;

public sealed record GetWarehouseDocumentHistoryQuery(Guid DocumentId)
    : IQuery<WarehouseDocumentHistoryResponse>;
