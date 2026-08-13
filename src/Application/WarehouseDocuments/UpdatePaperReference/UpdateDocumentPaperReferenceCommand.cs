using Application.Abstractions.Messaging;

namespace Application.WarehouseDocuments.UpdatePaperReference;

public sealed record UpdateDocumentPaperReferenceCommand(
    Guid DocumentId,
    string? PaperDocumentNumber,
    int? PaperDocumentYear,
    int ExpectedRowVersion) : ICommand;
