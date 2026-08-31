using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Sites;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.Sites.GetById;

internal sealed class GetSiteByIdQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    HybridCache hybridCache)
    : IQueryHandler<GetSiteByIdQuery, SiteResponse>
{
    public async Task<Result<SiteResponse>> Handle(GetSiteByIdQuery query, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionAsync(
            userContext.UserId,
            PermissionCodes.Sites.View,
            cancellationToken) && await scopeAuthorizationService.CanAccessSiteAsync(
            userContext.UserId,
            query.SiteId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<SiteResponse>(SiteErrors.NotFound(query.SiteId));
        }

        SiteResponse? site = await hybridCache.GetOrCreateAsync(
            $"sites:by-id:{query.SiteId}",
            async ct => await context.Sites
                .AsNoTracking()
                .Where(s => s.Id == query.SiteId)
                .Select(s => new SiteResponse
                {
                    Id = s.Id,
                    OrganizationId = s.OrganizationId,
                    Name = s.Name,
                    Code = s.Code,
                    Location = s.Location,
                    GovernorateCode = s.GovernorateCode,
                    Status = s.Status.ToString()
                })
                .SingleOrDefaultAsync(ct),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(10),
                LocalCacheExpiration = TimeSpan.FromMinutes(10)
            },
            tags: ["sites"],
            cancellationToken: cancellationToken);

        if (site is null)
        {
            return Result.Failure<SiteResponse>(SiteErrors.NotFound(query.SiteId));
        }

        return site;
    }
}
