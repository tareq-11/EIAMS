using Application.Abstractions.Messaging;

namespace Application.DocumentLines.Update;

public sealed record UpdateDocumentLineCommand(
    Guid DocumentId,
    Guid LineId,
    decimal Quantity,
    Guid? UnitId,
    decimal? UnitPrice,
    string? BatchNumber,
    DateOnly? ExpiryDate,
    int ExpectedRowVersion) : ICommand;
