using Application.Abstractions.Messaging;

namespace Application.ReturnInfos.Upsert;

public sealed record UpsertReturnInfoCommand(
    Guid DocumentId,
    Guid OriginalIssueDocumentId,
    string ReturnReason,
    int ExpectedRowVersion) : ICommand;
