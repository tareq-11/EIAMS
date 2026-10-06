using Application.Abstractions.Messaging;
using Domain.Users;

namespace Application.Users.SetStatus;

/// <summary>
/// Account lifecycle transition: activate or suspend.
/// <para>
/// This is its own operation rather than a field on the metadata update because
/// suspension is a security transition, not an edit. It carries its own guards and
/// it revokes the account's refresh tokens, none of which belong to a profile edit.
/// It also removes the previous trapdoor, where suspending a user required replaying
/// their whole profile — including a <c>username</c> the caller had to know but no
/// read projection disclosed.
/// </para>
/// </summary>
public sealed record SetUserStatusCommand(
    Guid UserId,
    UserStatus Status,
    int ExpectedRowVersion) : ICommand;