using Application.Abstractions.Messaging;
using Domain.Users;

namespace Application.Users.Update;

/// <summary>
/// Account METADATA only: display name and email.
/// <para>
/// It deliberately no longer carries <c>username</c> or <c>status</c>:
/// </para>
/// <list type="bullet">
/// <item>
/// <c>username</c> is the account's login identity and is immutable after creation. It
/// used to be required here while being returned by no read projection, so an
/// administrator could neither see the current value nor preserve it — any save
/// silently renamed the account. Removing it from the body means the binder has
/// nothing to write it to. Note that an unknown property is silently IGNORED rather
/// than rejected on this route; see <c>UpdateUserController</c> for the measured
/// behaviour.
/// </item>
/// <item>
/// <c>status</c> is a lifecycle transition with its own guards (self-suspension, the
/// last Enterprise Administrator, revoking refresh tokens) and now has its own
/// operation, <see cref="Application.Users.SetStatus.SetUserStatusCommand"/>. Folding
/// it into a broad upsert meant suspending an account required replaying its whole
/// profile — the same defect RESOLUTION-027 retired for roles.
/// </item>
/// </list>
/// <para>
/// <c>ExpectedRowVersion</c> is the concurrency token and is required. A stale value is
/// refused with 409 rather than overwriting a concurrent change, matching the uniform
/// rule every other versioned write in the aggregate already follows. The user
/// aggregate previously had no token at all, which made it the one versioned
/// aggregate where the last write silently won.
/// </para>
/// </summary>
public sealed record UpdateUserCommand(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    int ExpectedRowVersion) : ICommand;