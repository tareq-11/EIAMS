using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Searching;
using Application.ReceivingInfos;
using Domain.Common;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.ReceivingInfos.GetSupplierSuggestions;

internal sealed class GetSupplierSuggestionsQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService authorizationService)
    : IQueryHandler<GetSupplierSuggestionsQuery, IReadOnlyList<SupplierSuggestionResponse>>
{
    public async Task<Result<IReadOnlyList<SupplierSuggestionResponse>>> Handle(
        GetSupplierSuggestionsQuery query,
        CancellationToken cancellationToken)
    {
        _ = await authorizationService.GetWarehousePermissionScopeAsync(
            userContext.UserId, PermissionCodes.WarehouseDocuments.View, cancellationToken);
        string? search = SqlLikePattern.CreateContains(query.Search);

        List<SupplierSuggestionResponse> suppliers = await context.ExternalParties.AsNoTracking()
            .Where(party => party.Status == Status.Active)
            .Where(party => search == null ||
                EF.Functions.Like(party.NameAr, search, SqlLikePattern.EscapeCharacter) ||
                party.Code != null && EF.Functions.Like(party.Code, search, SqlLikePattern.EscapeCharacter))
            .OrderBy(party => party.NameAr)
            .ThenBy(party => party.Id)
            .Select(party => new SupplierSuggestionResponse(party.Id, party.NameAr, party.Code))
            .Take(20)
            .ToListAsync(cancellationToken);

        return suppliers;
    }
}
