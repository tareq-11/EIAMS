using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.WarehouseDocuments.CreateReversal;

public sealed record CreateReversalDocumentCommand(
    Guid SourceDocumentId,
    Guid? IdempotencyKey = null,
    DocumentType? RequiredDocumentType = null) : ICommand<Guid>;
