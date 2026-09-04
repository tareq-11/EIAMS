namespace Web.Api.Infrastructure;

internal static class AuthCookies
{
    public const string CookieName = "eiams_refresh_token";
    public const string LegacyCookieName = "refreshToken";
    public const string CookiePath = "/api/v1/auth";
    private const string LegacyCookiePath = "/users";

    public static void SetRefreshTokenCookie(HttpContext context, string refreshToken, int expirationDays = 7)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            Expires = DateTimeOffset.UtcNow.AddDays(expirationDays)
        };

        context.Response.Cookies.Append(CookieName, refreshToken, cookieOptions);
    }

    public static string? GetRefreshTokenFromCookieOrBody(HttpContext context, string? bodyToken)
    {
        if (!string.IsNullOrWhiteSpace(bodyToken))
        {
            return bodyToken.Trim();
        }

        if (context.Request.Cookies.TryGetValue(CookieName, out string? cookieToken) && !string.IsNullOrWhiteSpace(cookieToken))
        {
            return cookieToken.Trim();
        }

        if (context.Request.Cookies.TryGetValue(LegacyCookieName, out string? legacyCookieToken) && !string.IsNullOrWhiteSpace(legacyCookieToken))
        {
            return legacyCookieToken.Trim();
        }

        return null;
    }

    public static void ClearRefreshTokenCookies(HttpContext context)
    {
        DeleteCookie(context, CookieName, CookiePath);
        DeleteCookie(context, LegacyCookieName, CookiePath);

        // Remove cookies issued before the API was moved from /users to /api/v1/auth.
        DeleteCookie(context, CookieName, LegacyCookiePath);
        DeleteCookie(context, LegacyCookieName, LegacyCookiePath);
    }

    private static void DeleteCookie(HttpContext context, string name, string path)
    {
        context.Response.Cookies.Delete(name, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = path
        });
    }
}
