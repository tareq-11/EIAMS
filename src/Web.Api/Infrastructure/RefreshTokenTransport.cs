using Application.Users;
using Application.Users.GetSession;
using Microsoft.AspNetCore.Http.Features;

namespace Web.Api.Infrastructure;

public sealed class RefreshTokenTransport
{
    internal RefreshTokenResolution Resolve(HttpContext context)
    {
        if (RequestCanHaveBody(context))
        {
            return RefreshTokenResolution.Rejected(
                StatusCodes.Status400BadRequest,
                "REFRESH_TOKEN_BODY_DISABLED",
                "Refresh tokens must be supplied using the secure cookie.");
        }

        RefreshTokenResolution origin = ValidateCookieOrigin(context);
        if (!origin.IsAccepted)
        {
            return origin;
        }

        return RefreshTokenResolution.Accepted(GetCookieToken(context));
    }

    internal RefreshTokenResolution ValidateCookieOrigin(HttpContext context, bool requireOrigin = false)
    {
        if (!IsCookieOriginAllowed(context, requireOrigin))
        {
            return RefreshTokenResolution.Rejected(
                StatusCodes.Status403Forbidden,
                "REFRESH_TOKEN_ORIGIN_REJECTED",
                "The request origin is not allowed to use the refresh token cookie.");
        }

        return RefreshTokenResolution.Accepted(null);
    }

    private static bool RequestCanHaveBody(HttpContext context)
    {
        // Kestrel's body-detection feature accounts for chunked and HTTP/2 DATA frames,
        // where Content-Length is absent. Fail closed if the feature is unavailable.
        IHttpRequestBodyDetectionFeature? feature =
            context.Features.Get<IHttpRequestBodyDetectionFeature>();
        return feature is null || feature.CanHaveBody;
    }

    internal AuthenticationTokensResponse CreateResponse(AccessTokensResponse tokens) =>
        new(tokens.AccessToken, tokens.Session, GetExpiresInSeconds(tokens.AccessToken));

    private static int GetExpiresInSeconds(string accessToken)
    {
        string[] segments = accessToken.Split('.');
        if (segments.Length < 2)
        {
            return 0;
        }

        try
        {
            byte[] payload = Convert.FromBase64String(segments[1].Replace('-', '+').Replace('_', '/').PadRight((segments[1].Length + 3) / 4 * 4, '='));
            using var document = System.Text.Json.JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("exp", out System.Text.Json.JsonElement expiration) || !expiration.TryGetInt64(out long unixSeconds))
            {
                return 0;
            }

            return Math.Max(0, (int)Math.Min(int.MaxValue, unixSeconds - DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        }
        catch (FormatException) { return 0; }
        catch (System.Text.Json.JsonException) { return 0; }
    }

    private static bool IsCookieOriginAllowed(HttpContext context, bool requireOrigin)
    {
        string? fetchSite = context.Request.Headers["Sec-Fetch-Site"].FirstOrDefault();
        if (string.Equals(fetchSite, "cross-site", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? origin = context.Request.Headers.Origin.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(origin))
        {
            return !requireOrigin;
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

        return false;
    }

    private static string? GetCookieToken(HttpContext context)
    {
        if (context.Request.Cookies.TryGetValue(AuthCookies.CookieName, out string? token))
        {
            return Normalize(token);
        }

        return null;
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
    UserSessionResponse Session,
    int ExpiresInSeconds);
