using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Application.Abstractions.Recipients;
using SharedKernel;

namespace Application.Counterparts.GetList;

internal sealed class GetCounterpartsQueryHandler(
    ICounterpartResolver counterpartResolver,
    IUserContext userContext,
    IScopeAuthorizationService authorizationService)
    : IQueryHandler<GetCounterpartsQuery, PagedResult<CounterpartResolution>>
{
    public async Task<Result<PagedResult<CounterpartResolution>>> Handle(
        GetCounterpartsQuery query,
        CancellationToken cancellationToken)
    {
        bool authorized = await authorizationService.HasPermissionAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.View,
            cancellationToken);
        if (!authorized)
        {
            return Result.Failure<PagedResult<CounterpartResolution>>(CounterpartErrors.OutsideScope);
        }

        PagedResult<CounterpartResolution> result = await counterpartResolver.SearchActiveAsync(
            userContext.UserId,
            query.Search,
            query.Type,
            query.Page,
            query.PageSize,
            cancellationToken);
        return result;
    }
}
