using System.Text.Json.Serialization;
using Application.Users;

namespace Web.Api.Infrastructure;

public sealed class RefreshTokenTransportOptions
{
    internal const string SectionName = "Authentication:RefreshTokenTransport";

    public bool AllowRequestBody { get; init; } = true;

    public bool IncludeInResponseBody { get; init; } = true;

    public string[] AllowedCookieOrigins { get; init; } = [];
}

public sealed class RefreshTokenTransport(RefreshTokenTransportOptions options)
{
    internal RefreshTokenResolution Resolve(HttpContext context, string? bodyToken)
    {
        string? normalizedBodyToken = Normalize(bodyToken);
        string? cookieToken = GetCookieToken(context);

        if (normalizedBodyToken is not null && !options.AllowRequestBody)
        {
            return RefreshTokenResolution.Rejected(
                StatusCodes.Status400BadRequest,
                "REFRESH_TOKEN_BODY_DISABLED",
                "Refresh tokens must be supplied using the secure cookie.");
        }

        // During the compatibility window, an explicit body token selects that session even if
        // this HTTP client also holds a cookie for another device/session. Once body transport is
        // disabled, the branch above rejects it and the cookie becomes the only source.
        if (normalizedBodyToken is not null)
        {
            return RefreshTokenResolution.Accepted(normalizedBodyToken);
        }

        if (cookieToken is not null && !IsCookieOriginAllowed(context))
        {
            return RefreshTokenResolution.Rejected(
                StatusCodes.Status403Forbidden,
                "REFRESH_TOKEN_ORIGIN_REJECTED",
                "The request origin is not allowed to use the refresh token cookie.");
        }

        return RefreshTokenResolution.Accepted(cookieToken);
    }

    internal AuthenticationTokensResponse CreateResponse(AccessTokensResponse tokens) =>
        new(tokens.AccessToken, options.IncludeInResponseBody ? tokens.RefreshToken : null);

    private bool IsCookieOriginAllowed(HttpContext context)
    {
        string? fetchSite = context.Request.Headers["Sec-Fetch-Site"].FirstOrDefault();
        if (string.Equals(fetchSite, "cross-site", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? origin = context.Request.Headers.Origin.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(origin))
        {
            return true;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? originUri) ||
            !string.IsNullOrEmpty(originUri.UserInfo) ||
            originUri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(originUri.Query) ||
            !string.IsNullOrEmpty(originUri.Fragment))
        {
            return false;
        }

        string requestOrigin = $"{context.Request.Scheme}://{context.Request.Host}";
        if (string.Equals(origin.TrimEnd('/'), requestOrigin, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return options.AllowedCookieOrigins.Any(allowed =>
            string.Equals(allowed.TrimEnd('/'), origin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetCookieToken(HttpContext context)
    {
        if (context.Request.Cookies.TryGetValue(AuthCookies.CookieName, out string? token))
        {
            return Normalize(token);
        }

        return context.Request.Cookies.TryGetValue(AuthCookies.LegacyCookieName, out string? legacyToken)
            ? Normalize(legacyToken)
            : null;
    }

    private static string? Normalize(string? token) =>
        string.IsNullOrWhiteSpace(token) ? null : token.Trim();

}

internal sealed record RefreshTokenResolution(
    bool IsAccepted,
    string? Token,
    int ErrorStatusCode,
    string? ErrorCode,
    string? ErrorMessage)
{
    internal static RefreshTokenResolution Accepted(string? token) =>
        new(true, token, 0, null, null);

    internal static RefreshTokenResolution Rejected(int statusCode, string code, string message) =>
        new(false, null, statusCode, code, message);
}

public sealed record AuthenticationTokensResponse(
    string AccessToken,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RefreshToken);
