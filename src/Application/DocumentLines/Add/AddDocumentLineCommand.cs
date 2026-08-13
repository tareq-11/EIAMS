using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.DocumentLines.Add;

public sealed record AddDocumentLineCommand(
    Guid DocumentId,
    Guid MaterialId,
    decimal Quantity,
    Guid? UnitId,
    decimal? UnitPrice,
    string? BatchNumber,
    DateOnly? ExpiryDate,
    OpeningType? OpeningType,
    int ExpectedRowVersion) : ICommand<Guid>;
