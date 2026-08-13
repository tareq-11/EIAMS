using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.IssueTos.Upsert;

public sealed record UpsertIssueToCommand(
    Guid DocumentId,
    PartyType RecipientType,
    Guid RecipientId,
    string IssueReason,
    int ExpectedRowVersion) : ICommand;
