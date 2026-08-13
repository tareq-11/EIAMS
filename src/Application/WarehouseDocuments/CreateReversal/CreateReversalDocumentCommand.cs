using Application.Abstractions.Messaging;

namespace Application.WarehouseDocuments.CreateReversal;

public sealed record CreateReversalDocumentCommand(Guid SourceDocumentId) : ICommand<Guid>;
