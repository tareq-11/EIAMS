using Application.Abstractions.Messaging;

namespace Application.WarehouseDocuments.Reject;

public sealed record RejectDocumentCommand(Guid DocumentId, int ExpectedRowVersion) : ICommand;
