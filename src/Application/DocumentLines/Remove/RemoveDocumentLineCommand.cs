using Application.Abstractions.Messaging;

namespace Application.DocumentLines.Remove;

public sealed record RemoveDocumentLineCommand(Guid DocumentId, Guid LineId, int ExpectedRowVersion) : ICommand;
