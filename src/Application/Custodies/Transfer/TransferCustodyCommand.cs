using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.Custodies.Transfer;

public sealed record TransferCustodyCommand(
    Guid CustodyId,
    CustodySubjectType SubjectType,
    PartyType NewHolderType,
    Guid NewHolderId,
    CustodyKind NewCustodyKind,
    int ExpectedRowVersion,
    string? Note = null) : ICommand;
