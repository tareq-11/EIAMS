using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Abstractions.Recipients;
using SharedKernel;

namespace Application.Counterparts.GetById;

internal sealed class GetCounterpartByIdQueryHandler(
    ICounterpartResolver counterpartResolver,
    IUserContext userContext,
    IScopeAuthorizationService authorizationService)
    : IQueryHandler<GetCounterpartByIdQuery, CounterpartResolution>
{
    public async Task<Result<CounterpartResolution>> Handle(
        GetCounterpartByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(query.Type))
        {
            return Result.Failure<CounterpartResolution>(CounterpartErrors.TypeInvalid);
        }

        CounterpartResolution? counterpart = await counterpartResolver.ResolveAsync(
            query.Type, query.CounterpartId, cancellationToken);
        if (counterpart is null)
        {
            return Result.Failure<CounterpartResolution>(
                CounterpartErrors.NotFound(query.Type, query.CounterpartId));
        }

        bool hasReadPermission = await authorizationService.HasPermissionAsync(
            userContext.UserId, PermissionCodes.WarehouseDocuments.View, cancellationToken);
        bool insideScope = await authorizationService.CanAccessPartyAsync(
            userContext.UserId, query.Type, query.CounterpartId, cancellationToken);

        return hasReadPermission && insideScope
            ? counterpart
            : Result.Failure<CounterpartResolution>(CounterpartErrors.OutsideScope);
    }
}
