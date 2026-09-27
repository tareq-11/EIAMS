using Application.Users.GetSession;

namespace Application.Users;

public sealed record AccessTokensResponse(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    UserSessionResponse Session);
