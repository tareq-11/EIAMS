using Application.Abstractions.Messaging;

namespace Application.WarehouseDocuments.Post;

public sealed record PostDocumentCommand(Guid DocumentId, int ExpectedRowVersion) : ICommand;
