using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.WarehouseDocuments.Post;

public sealed record PostDocumentCommand(
    Guid DocumentId,
    int ExpectedRowVersion,
    Guid? IdempotencyKey = null,
    DocumentType? RequiredDocumentType = null) : ICommand<PostDocumentResponse>;

public sealed record PostDocumentWarningResponse(
    string Code,
    string Message,
    Guid CountId,
    Guid WarehouseId);

public sealed record PostDocumentResponse(
    Guid DocumentId,
    IReadOnlyList<PostDocumentWarningResponse> Warnings);
