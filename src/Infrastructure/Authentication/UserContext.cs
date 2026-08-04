using System.Security.Claims;
using Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Authentication;

internal sealed class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId =>
        _httpContextAccessor
            .HttpContext?
            .User
            .GetUserId() ??
        throw new UserContextUnavailableException();

    public Guid? UserIdOrDefault
    {
        get
        {
            ClaimsPrincipal? user = _httpContextAccessor.HttpContext?.User;

            if (user is not { Identity.IsAuthenticated: true })
            {
                return null;
            }

            return user.GetUserId();
        }
    }
}
