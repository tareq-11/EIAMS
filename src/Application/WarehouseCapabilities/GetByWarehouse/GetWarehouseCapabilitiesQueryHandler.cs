using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;
using Domain.WarehouseCapabilities;
using Domain.WarehouseCapabilityOperations;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseCapabilities.GetByWarehouse;

internal sealed class GetWarehouseCapabilitiesQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : IQueryHandler<GetWarehouseCapabilitiesQuery, PagedResult<WarehouseCapabilityResponse>>
{
    public async Task<Result<PagedResult<WarehouseCapabilityResponse>>> Handle(
        GetWarehouseCapabilitiesQuery query,
        CancellationToken cancellationToken)
    {
        if (!await context.Warehouses.AnyAsync(w => w.Id == query.WarehouseId, cancellationToken))
        {
            return Result.Failure<PagedResult<WarehouseCapabilityResponse>>(
                WarehouseErrors.NotFound(query.WarehouseId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Warehouses.View,
            ScopeType.Warehouse,
            query.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<PagedResult<WarehouseCapabilityResponse>>(WarehouseCapabilityErrors.Forbidden);
        }

        PagedResult<WarehouseCapabilityResponse> capabilities = await (
                from capability in context.WarehouseCapabilities
                where capability.WarehouseId == query.WarehouseId
                join materialDomain in context.MaterialDomains
                    on capability.MaterialDomainId equals materialDomain.Id
                select new WarehouseCapabilityResponse
                {
                    Id = capability.Id,
                    WarehouseId = capability.WarehouseId,
                    MaterialDomainId = capability.MaterialDomainId,
                    MaterialDomainCode = materialDomain.Code,
                    MaterialDomainName = materialDomain.Name,
                    Status = capability.Status.ToString()
                })
            .OrderBy(c => c.MaterialDomainCode)
            .ThenBy(c => c.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);

        await AttachOperationsAsync(capabilities.Items, cancellationToken);

        return capabilities;
    }

    /// <summary>
    /// Attaches each capability's granted operations to the already-paged result.
    /// </summary>
    /// <remarks>
    /// Two phases rather than one join, for two reasons. EF Core cannot project a
    /// collection across a paged Skip/Take, so a collection-valued projection inside the
    /// paged query is not expressible. And a join against the operation child table would
    /// fan out the capability rows, inflating the count that drives TotalItems and
    /// therefore TotalPages - paging would silently skip and repeat capabilities.
    ///
    /// Keyed strictly on the ids of the current page, so the second query stays bounded by
    /// page size and cannot change the page count.
    ///
    /// Ordered by the enum value in memory, which is the OperationType declaration order
    /// (Receiving, Issue, Transfer, Count, Return, Adjustment), and only then projected to its
    /// wire string. Ordering the projected strings would sort the underlying string values
    /// alphabetically instead, because the column is mapped with HasConversion&lt;string&gt; and
    /// because OperationType is written as an ordinal by the minimal-API serialiser.
    /// </remarks>
    private async Task AttachOperationsAsync(
        IReadOnlyList<WarehouseCapabilityResponse> capabilities,
        CancellationToken cancellationToken)
    {
        if (capabilities.Count == 0)
        {
            return;
        }

        Guid[] capabilityIds = [.. capabilities.Select(capability => capability.Id)];

        List<WarehouseCapabilityOperation> operations = await context.WarehouseCapabilityOperations
            .AsNoTracking()
            .Where(operation => capabilityIds.Contains(operation.CapabilityId))
            .ToListAsync(cancellationToken);

        var operationsByCapabilityId = operations
            .GroupBy(operation => operation.CapabilityId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)[.. group
                    .OrderBy(operation => operation.OperationType)
                    .Select(operation => operation.OperationType.ToString())]);

        foreach (WarehouseCapabilityResponse capability in capabilities)
        {
            if (operationsByCapabilityId.TryGetValue(capability.Id, out IReadOnlyList<string>? granted))
            {
                capability.Operations = granted;
            }
        }
    }
}