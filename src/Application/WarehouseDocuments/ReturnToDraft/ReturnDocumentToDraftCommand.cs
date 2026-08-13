using Application.Abstractions.Messaging;

namespace Application.WarehouseDocuments.ReturnToDraft;

public sealed record ReturnDocumentToDraftCommand(Guid DocumentId, int ExpectedRowVersion) : ICommand;
