using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.DocumentLines.Update;

public sealed record UpdateDocumentLineCommand(
    Guid DocumentId,
    Guid LineId,
    decimal Quantity,
    Guid? UnitId,
    decimal? UnitPrice,
    string? BatchNumber,
    DateOnly? ExpiryDate,
    OpeningType? OpeningType,
    int ExpectedRowVersion) : ICommand;
