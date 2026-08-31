using Application.Abstractions.Messaging;

namespace Application.WarehouseDocuments.CreateReversal;

public sealed record CreateReversalDocumentCommand(Guid SourceDocumentId, Guid? IdempotencyKey = null) : ICommand<Guid>;
