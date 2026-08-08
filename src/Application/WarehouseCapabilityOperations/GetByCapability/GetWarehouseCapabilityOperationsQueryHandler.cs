using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.WarehouseCapabilities;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseCapabilityOperations.GetByCapability;

internal sealed class GetWarehouseCapabilityOperationsQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetWarehouseCapabilityOperationsQuery, List<WarehouseCapabilityOperationResponse>>
{
    public async Task<Result<List<WarehouseCapabilityOperationResponse>>> Handle(
        GetWarehouseCapabilityOperationsQuery query,
        CancellationToken cancellationToken)
    {
        WarehouseCapability? capability = await context.WarehouseCapabilities
            .SingleOrDefaultAsync(c => c.Id == query.CapabilityId, cancellationToken);

        if (capability is null)
        {
            return Result.Failure<List<WarehouseCapabilityOperationResponse>>(
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
            return Result.Failure<List<WarehouseCapabilityOperationResponse>>(WarehouseCapabilityErrors.Forbidden);
        }

        List<WarehouseCapabilityOperationResponse> operations = await context.WarehouseCapabilityOperations
            .Where(o => o.CapabilityId == query.CapabilityId)
            .Select(o => new WarehouseCapabilityOperationResponse
            {
                Id = o.Id,
                CapabilityId = o.CapabilityId,
                OperationType = o.OperationType.ToString()
            })
            .ToListAsync(cancellationToken);

        return operations;
    }
}
