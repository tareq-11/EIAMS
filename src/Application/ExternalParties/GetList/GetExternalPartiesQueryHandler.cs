using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.ExternalParties.GetList;

internal sealed class GetExternalPartiesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetExternalPartiesQuery, PagedResult<ExternalPartyResponse>>
{
    public async Task<Result<PagedResult<ExternalPartyResponse>>> Handle(
        GetExternalPartiesQuery query,
        CancellationToken cancellationToken)
    {
        string? search = string.IsNullOrWhiteSpace(query.Search)
            ? null
            : query.Search.Trim().ToUpperInvariant();

        PagedResult<ExternalPartyResponse> parties = await context.ExternalParties
            .AsNoTracking()
            .Where(item => query.Status == null || item.Status == query.Status)
            .Where(item => search == null ||
                item.NormalizedNameAr.Contains(search) ||
                item.NormalizedCode != null && item.NormalizedCode.Contains(search))
            .OrderBy(item => item.NameAr)
            .ThenBy(item => item.Id)
            .ToPagedResultAsync(item => new ExternalPartyResponse(
                item.Id,
                item.NameAr,
                item.Code,
                item.ContactInfo,
                item.Notes,
                item.Status.ToString(),
                item.RowVersion,
                item.CreatedAtUtc,
                item.UpdatedAtUtc),
                query.Page,
                query.PageSize,
                cancellationToken);

        return parties;
    }
}
