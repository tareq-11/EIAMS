namespace Web.Api.Infrastructure;

internal static class AuthCookies
{
    public const string CookieName = "eiams_refresh_token";
    public const string LegacyCookieName = "refreshToken";
    public const string CookiePath = "/users";

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
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            Expires = DateTimeOffset.UtcNow.AddDays(-1)
        };

        context.Response.Cookies.Delete(CookieName, cookieOptions);
        context.Response.Cookies.Delete(LegacyCookieName, cookieOptions);
    }
}
