using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.WarehouseDocuments.Create;

public sealed record CreateWarehouseDocumentCommand(Guid WarehouseId, DocumentType DocumentType) : ICommand<Guid>;
