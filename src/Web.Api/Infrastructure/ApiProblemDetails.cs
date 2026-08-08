using Microsoft.AspNetCore.Mvc;

namespace Web.Api.Infrastructure;

internal static class ApiProblemDetails
{
    internal static IActionResult CreateValidationResponse(ActionContext context)
    {
        var details = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "The submitted value is invalid."
                        : error.ErrorMessage)
                    .ToArray());

        ApiErrorResponse response = ApiResults.CreateErrorResponse(
            context.HttpContext,
            "REQUEST_VALIDATION_FAILED",
            "One or more request values are invalid.",
            details);

        return new BadRequestObjectResult(response);
    }
}
