using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.ReceivingInfos.GetSupplierSuggestions;

internal sealed class GetSupplierSuggestionsQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService authorizationService)
    : IQueryHandler<GetSupplierSuggestionsQuery, IReadOnlyList<string>>
{
    public async Task<Result<IReadOnlyList<string>>> Handle(
        GetSupplierSuggestionsQuery query,
        CancellationToken cancellationToken)
    {
        WarehousePermissionScope scope = await authorizationService.GetWarehousePermissionScopeAsync(
            userContext.UserId, PermissionCodes.WarehouseDocuments.View, cancellationToken);
        string? search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        List<string> suppliers = await (
                from info in context.ReceivingInfos.AsNoTracking()
                join document in context.WarehouseDocuments.AsNoTracking()
                    on info.Id equals document.Id
                where scope.HasEnterpriseAccess || scope.WarehouseIds.Contains(document.WarehouseId)
                where search == null || EF.Functions.Like(info.SupplierRef, $"%{search}%")
                select info.SupplierRef)
            .Distinct()
            .OrderBy(value => value)
            .Take(20)
            .ToListAsync(cancellationToken);

        return suppliers;
    }
}
