using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;
using Domain.WarehouseCapabilities;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseCapabilityOperations.GetByCapability;

internal sealed class GetWarehouseCapabilityOperationsQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetWarehouseCapabilityOperationsQuery, PagedResult<WarehouseCapabilityOperationResponse>>
{
    public async Task<Result<PagedResult<WarehouseCapabilityOperationResponse>>> Handle(
        GetWarehouseCapabilityOperationsQuery query,
        CancellationToken cancellationToken)
    {
        WarehouseCapability? capability = await context.WarehouseCapabilities
            .SingleOrDefaultAsync(c => c.Id == query.CapabilityId, cancellationToken);

        if (capability is null)
        {
            return Result.Failure<PagedResult<WarehouseCapabilityOperationResponse>>(
                WarehouseCapabilityErrors.NotFound(query.CapabilityId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseCapabilities.Manage,
            ScopeType.Warehouse,
            capability.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<PagedResult<WarehouseCapabilityOperationResponse>>(
                WarehouseCapabilityErrors.Forbidden);
        }

        PagedResult<WarehouseCapabilityOperationResponse> operations = await context.WarehouseCapabilityOperations
            .Where(o => o.CapabilityId == query.CapabilityId)
            .Select(o => new WarehouseCapabilityOperationResponse
            {
                Id = o.Id,
                CapabilityId = o.CapabilityId,
                OperationType = o.OperationType.ToString()
            })
            .OrderBy(o => o.OperationType)
            .ThenBy(o => o.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        return operations;
    }
}
