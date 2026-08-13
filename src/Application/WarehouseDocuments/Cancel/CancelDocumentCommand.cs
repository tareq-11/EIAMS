using Application.Abstractions.Messaging;

namespace Application.WarehouseDocuments.Cancel;

public sealed record CancelDocumentCommand(Guid DocumentId, int ExpectedRowVersion) : ICommand;
