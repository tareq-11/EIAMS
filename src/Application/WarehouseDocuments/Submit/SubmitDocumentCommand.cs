using Application.Abstractions.Messaging;

namespace Application.WarehouseDocuments.Submit;

public sealed record SubmitDocumentCommand(Guid DocumentId, int ExpectedRowVersion) : ICommand;
