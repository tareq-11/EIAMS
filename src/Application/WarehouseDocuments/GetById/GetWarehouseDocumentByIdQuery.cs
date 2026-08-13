using Application.Abstractions.Messaging;

namespace Application.WarehouseDocuments.GetById;

public sealed record GetWarehouseDocumentByIdQuery(Guid DocumentId) : IQuery<WarehouseDocumentDetailsResponse>;
