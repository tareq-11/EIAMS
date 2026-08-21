using Application.Abstractions.Messaging;

namespace Application.WarehouseDocuments.Post;

public sealed record PostDocumentCommand(Guid DocumentId, int ExpectedRowVersion) : ICommand<PostDocumentResponse>;

public sealed record PostDocumentWarningResponse(
    string Code,
    string Message,
    Guid CountId,
    Guid WarehouseId);

public sealed record PostDocumentResponse(
    Guid DocumentId,
    IReadOnlyList<PostDocumentWarningResponse> Warnings);
