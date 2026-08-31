using Application.Abstractions.Messaging;

namespace Application.Users.GetSession;

public sealed record GetUserSessionQuery(Guid UserId) : IQuery<UserSessionResponse>;
