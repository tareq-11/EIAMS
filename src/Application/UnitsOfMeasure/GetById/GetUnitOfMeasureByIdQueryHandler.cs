using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.UnitsOfMeasure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SharedKernel;

namespace Application.UnitsOfMeasure.GetById;

internal sealed class GetUnitOfMeasureByIdQueryHandler(
    IApplicationDbContext context,
    HybridCache hybridCache)
    : IQueryHandler<GetUnitOfMeasureByIdQuery, UnitOfMeasureResponse>
{
    public async Task<Result<UnitOfMeasureResponse>> Handle(
        GetUnitOfMeasureByIdQuery query,
        CancellationToken cancellationToken)
    {
        UnitOfMeasureResponse? unit = await hybridCache.GetOrCreateAsync(
            $"uom:by-id:{query.UnitOfMeasureId}",
            async ct => await context.UnitsOfMeasure
                .AsNoTracking()
                .Where(u => u.Id == query.UnitOfMeasureId)
                .Select(u => new UnitOfMeasureResponse
                {
                    Id = u.Id,
                    Name = u.Name,
                    Symbol = u.Symbol,
                    UnitType = u.UnitType
                })
                .SingleOrDefaultAsync(ct),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(10),
                LocalCacheExpiration = TimeSpan.FromMinutes(10)
            },
            tags: ["uom"],
            cancellationToken: cancellationToken);

        if (unit is null)
        {
            return Result.Failure<UnitOfMeasureResponse>(UnitOfMeasureErrors.NotFound(query.UnitOfMeasureId));
        }

        return unit;
    }
}
