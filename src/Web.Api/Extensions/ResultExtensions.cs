using Application.Abstractions.Pagination;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Extensions;

public static class ResultExtensions
{
    public static IResult ToApiResponse(this Result result, HttpContext context)
    {
        return result.IsSuccess
            ? ApiResults.Success(context)
            : CustomResults.Problem(result, context);
    }

    public static IResult ToApiResponse<T>(this Result<T> result, HttpContext context)
    {
        return result.IsSuccess
            ? ApiResults.Ok(context, result.Value)
            : CustomResults.Problem(result, context);
    }

    public static IResult ToPaginatedApiResponse<T>(
        this Result<PagedResult<T>> result,
        HttpContext context)
    {
        if (result.IsFailure)
        {
            return CustomResults.Problem(result, context);
        }

        PagedResult<T> page = result.Value;

        return ApiResults.Ok(
            context,
            page.Items,
            new ApiPagination(page.Page, page.PageSize, page.TotalItems, page.TotalPages));
    }

    public static IResult ToApiResponse(this Result<Guid> result, HttpContext context)
    {
        return result.IsSuccess
            ? ApiResults.Ok(context, new ResourceIdResponse(result.Value))
            : CustomResults.Problem(result, context);
    }

    public static IResult ToCreatedApiResponse(
        this Result<Guid> result,
        HttpContext context,
        Func<Guid, string> locationFactory)
    {
        return result.IsSuccess
            ? ApiResults.Created(
                context,
                locationFactory(result.Value),
                new ResourceIdResponse(result.Value))
            : CustomResults.Problem(result, context);
    }

    public static TOut Match<TOut>(
        this Result result,
        Func<TOut> onSuccess,
        Func<Result, TOut> onFailure)
    {
        return result.IsSuccess ? onSuccess() : onFailure(result);
    }

    public static TOut Match<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, TOut> onSuccess,
        Func<Result<TIn>, TOut> onFailure)
    {
        return result.IsSuccess ? onSuccess(result.Value) : onFailure(result);
    }
}
