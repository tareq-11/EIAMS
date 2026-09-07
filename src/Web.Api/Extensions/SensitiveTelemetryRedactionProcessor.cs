using System.Diagnostics;
using OpenTelemetry;

namespace Web.Api.Extensions;

internal sealed class SensitiveTelemetryRedactionProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity data)
    {
        RedactAbsoluteUrl(data, "url.full");
        RedactAbsoluteUrl(data, "http.url");
        data.SetTag("url.query", null);
        RedactHttpTarget(data);
        data.SetTag("db.query.text", null);
        data.SetTag("db.statement", null);
    }

    private static void RedactAbsoluteUrl(Activity activity, string tagName)
    {
        if (activity.GetTagItem(tagName) is not string value ||
            !Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            !IsSafeHttpUrl(uri))
        {
            activity.SetTag(tagName, null);
            return;
        }

        activity.SetTag(tagName, uri.GetLeftPart(UriPartial.Path));
    }

    private static void RedactHttpTarget(Activity activity)
    {
        if (activity.GetTagItem("http.target") is not string value ||
            !TryGetSafePath(value, out string? path))
        {
            activity.SetTag("http.target", null);
            return;
        }

        activity.SetTag("http.target", path);
    }

    private static bool IsSafeHttpUrl(Uri uri) =>
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
        !string.IsNullOrEmpty(uri.Host) &&
        string.IsNullOrEmpty(uri.UserInfo);

    private static bool TryGetSafePath(string value, out string? path)
    {
        path = null;

        if (string.IsNullOrWhiteSpace(value) ||
            value[0] != '/' ||
            value.StartsWith("//", StringComparison.Ordinal) ||
            value.Contains('\\') ||
            value.Any(char.IsControl))
        {
            return false;
        }

        int queryOrFragmentIndex = value.IndexOfAny(['?', '#']);
        string candidate = queryOrFragmentIndex >= 0 ? value[..queryOrFragmentIndex] : value;

        if (!Uri.TryCreate($"https://telemetry.invalid{candidate}", UriKind.Absolute, out Uri? uri) ||
            uri.GetComponents(UriComponents.Path, UriFormat.UriEscaped) is not string escapedPath ||
            !string.Equals($"/{escapedPath}", candidate, StringComparison.Ordinal))
        {
            return false;
        }

        path = candidate;
        return true;
    }
}
