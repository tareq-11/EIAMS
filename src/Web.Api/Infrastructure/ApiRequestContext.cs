namespace Web.Api.Infrastructure;

internal static class ApiRequestContext
{
    internal const string RequestIdItemKey = "ApiRequestId";

    internal static string GetRequestId(HttpContext context)
    {
        if (context.Items.TryGetValue(RequestIdItemKey, out object? value) && value is string requestId)
        {
            return requestId;
        }

        requestId = Guid.NewGuid().ToString();
        context.Items[RequestIdItemKey] = requestId;

        return requestId;
    }
}
