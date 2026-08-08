using SharedKernel;

namespace Web.Api.Infrastructure;

public static class CustomResults
{
    public static IResult Problem(Result result, HttpContext context)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException();
        }

        object? details = result.Error is ValidationError validationError
            ? new { errors = validationError.Errors }
            : result.Error.Details;

        return ApiResults.Error(
            context,
            GetStatusCode(result.Error.Type),
            result.Error.Code,
            result.Error.Description,
            details);
    }

    private static int GetStatusCode(ErrorType errorType) =>
        errorType switch
        {
            ErrorType.Validation or ErrorType.Problem => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };
}
