using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Web.Api.Infrastructure;

internal static class ApiProblemDetails
{
    private const string InvalidFormatMessage = "The submitted value has an invalid format.";

    internal static IActionResult CreateValidationResponse(ActionContext context)
    {
        KeyValuePair<string, ModelStateEntry?>[] invalidEntries = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToArray();

        bool hasJsonInputError = invalidEntries.Any(entry => IsJsonPath(entry.Key));

        var details = invalidEntries
            .Where(entry => !hasJsonInputError || !IsRequestParameter(entry.Key))
            .GroupBy(entry => NormalizeKey(entry.Key), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .SelectMany(entry => entry.Value!.Errors)
                    .Select(SanitizeMessage)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);

        ApiErrorResponse response = ApiResults.CreateErrorResponse(
            context.HttpContext,
            "REQUEST_VALIDATION_FAILED",
            "One or more request values are invalid.",
            details);

        return new BadRequestObjectResult(response);
    }

    private static string NormalizeKey(string key)
    {
        if (key.Equals("$", StringComparison.Ordinal))
        {
            return "body";
        }

        return key.StartsWith("$.", StringComparison.Ordinal)
            ? key[2..]
            : key;
    }

    private static string SanitizeMessage(ModelError error)
    {
        if (error.Exception is not null || IsTechnicalMessage(error.ErrorMessage))
        {
            return InvalidFormatMessage;
        }

        return string.IsNullOrWhiteSpace(error.ErrorMessage)
            ? "The submitted value is invalid."
            : error.ErrorMessage;
    }

    private static bool IsJsonPath(string key) =>
        key.StartsWith('$');

    private static bool IsRequestParameter(string key) =>
        key.Equals("request", StringComparison.OrdinalIgnoreCase);

    private static bool IsTechnicalMessage(string message) =>
        message.Contains("could not be converted to", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("JSON deserialization", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("Path:", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("LineNumber:", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("BytePositionInLine:", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("System.", StringComparison.Ordinal) ||
        message.Contains("Domain.", StringComparison.Ordinal);
}
