using System.Text;

namespace Web.Api.Infrastructure;

internal static class ApiResults
{
    internal static IResult Ok<T>(HttpContext context, T data, ApiPagination? pagination = null) =>
        Results.Ok(CreateSuccessResponse(context, data, pagination));

    internal static IResult Success(HttpContext context) =>
        Results.Ok(new ApiResponse<EmptyResponse>(
            Success: true,
            Data: null,
            Pagination: null,
            Meta: new ApiResponseMeta(
                ApiRequestContext.GetRequestId(context),
                DateTime.UtcNow)));

    internal static IResult Created<T>(HttpContext context, string location, T data) =>
        Results.Created(location, CreateSuccessResponse(context, data));

    internal static IResult Error(
        HttpContext context,
        int statusCode,
        string code,
        string message,
        object? details = null) =>
        Results.Json(
            CreateErrorResponse(context, code, message, details),
            statusCode: statusCode);

    internal static IResult ErrorFromStatusCode(HttpContext context, int statusCode)
    {
        (string Code, string Message) error = statusCode switch
        {
            StatusCodes.Status400BadRequest => ("REQUEST_INVALID", "The request is invalid. Review the submitted values and try again."),
            StatusCodes.Status401Unauthorized => ("AUTHENTICATION_REQUIRED", "Authentication is required. Provide a valid bearer token and try again."),
            StatusCodes.Status403Forbidden => ("AUTHORIZATION_FORBIDDEN", "You do not have the required permission to perform this action."),
            StatusCodes.Status404NotFound => ("RESOURCE_NOT_FOUND", "The requested resource or API route was not found."),
            StatusCodes.Status405MethodNotAllowed => ("METHOD_NOT_ALLOWED", "The HTTP method used is not allowed for this API route."),
            StatusCodes.Status409Conflict => ("RESOURCE_CONFLICT", "The request conflicts with the current state of the resource."),
            StatusCodes.Status413PayloadTooLarge => ("REQUEST_BODY_TOO_LARGE", "The request body exceeds the configured maximum size."),
            StatusCodes.Status415UnsupportedMediaType => ("UNSUPPORTED_MEDIA_TYPE", "The request body uses an unsupported content type."),
            StatusCodes.Status422UnprocessableEntity => ("UNPROCESSABLE_ENTITY", "The request values could not be processed."),
            StatusCodes.Status429TooManyRequests => ("RATE_LIMIT_EXCEEDED", "Too many requests were sent. Wait before trying again."),
            StatusCodes.Status500InternalServerError => ("SERVER_FAILURE", "An unexpected server error occurred."),
            StatusCodes.Status503ServiceUnavailable => ("SERVICE_UNAVAILABLE", "The service is temporarily unavailable. Try again later."),
            _ => ("REQUEST_FAILED", "The request could not be completed.")
        };

        return Error(context, statusCode, error.Code, error.Message);
    }

    internal static ApiErrorResponse CreateErrorResponse(
        HttpContext context,
        string code,
        string message,
        object? details = null) =>
        new(
            Success: false,
            Error: new ApiError(
                NormalizeErrorCode(code),
                message,
                details ?? new Dictionary<string, object?>(),
                ApiRequestContext.GetRequestId(context)));

    private static ApiResponse<T> CreateSuccessResponse<T>(
        HttpContext context,
        T data,
        ApiPagination? pagination = null) =>
        new(
            Success: true,
            Data: data,
            Pagination: pagination,
            Meta: new ApiResponseMeta(
                ApiRequestContext.GetRequestId(context),
                DateTime.UtcNow));

    private static string NormalizeErrorCode(string code)
    {
        var builder = new StringBuilder(code.Length + 8);

        for (int index = 0; index < code.Length; index++)
        {
            char character = code[index];

            if (!char.IsLetterOrDigit(character))
            {
                AppendSeparator(builder);
                continue;
            }

            if (char.IsUpper(character) && index > 0 && char.IsLower(code[index - 1]))
            {
                AppendSeparator(builder);
            }

            builder.Append(char.ToUpperInvariant(character));
        }

        return builder.ToString().Trim('_');
    }

    private static void AppendSeparator(StringBuilder builder)
    {
        if (builder.Length > 0 && builder[^1] != '_')
        {
            builder.Append('_');
        }
    }
}
