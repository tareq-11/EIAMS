using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Pagination;
using Application.Abstractions.Recipients;
using Domain.Common;
using Domain.Custodies;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.Recipients;

internal sealed class CounterpartResolver(
    IApplicationDbContext context,
    IScopeAuthorizationService scopeAuthorizationService) : ICounterpartResolver
{
    public async Task<CounterpartResolution?> ResolveAsync(
        PartyType type,
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(type) || id == Guid.Empty)
        {
            return null;
        }

        return type switch
        {
            PartyType.Employee => await context.Employees.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new CounterpartResolution(
                    type, item.Id, item.FullName, item.JobTitle, item.Status))
                .SingleOrDefaultAsync(cancellationToken),
            PartyType.OrganizationalUnit => await context.OrganizationalUnits.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new CounterpartResolution(
                    type, item.Id, item.Name, item.UnitType, item.Status))
                .SingleOrDefaultAsync(cancellationToken),
            PartyType.Site => await context.Sites.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new CounterpartResolution(
                    type, item.Id, item.Name, item.Code, item.Status))
                .SingleOrDefaultAsync(cancellationToken),
            PartyType.External => await context.ExternalParties.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new CounterpartResolution(
                    type, item.Id, item.NameAr, item.Code, item.Status))
                .SingleOrDefaultAsync(cancellationToken),
            _ => null
        };
    }

    public async Task<Result<CounterpartResolution>> ValidateForWriteAsync(
        Guid userId,
        PartyType type,
        Guid id,
        CustodyKind? custodyKind,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(type))
        {
            return Result.Failure<CounterpartResolution>(CounterpartErrors.TypeInvalid);
        }

        CounterpartResolution? counterpart = await ResolveAsync(type, id, cancellationToken);

        if (counterpart is null)
        {
            return Result.Failure<CounterpartResolution>(CounterpartErrors.NotFound(type, id));
        }

        if (counterpart.Status != Status.Active)
        {
            return Result.Failure<CounterpartResolution>(CounterpartErrors.Inactive(type, id));
        }

        bool insideScope = await scopeAuthorizationService.CanAccessPartyAsync(
            userId, type, id, cancellationToken);

        if (!insideScope)
        {
            return Result.Failure<CounterpartResolution>(CounterpartErrors.OutsideScope);
        }

        if (custodyKind == CustodyKind.Personal && type != PartyType.Employee)
        {
            return Result.Failure<CounterpartResolution>(CounterpartErrors.PersonalRequiresEmployee);
        }

        if (custodyKind == CustodyKind.Operational && type == PartyType.Employee)
        {
            return Result.Failure<CounterpartResolution>(CounterpartErrors.OperationalRequiresNonEmployee);
        }

        return counterpart;
    }

    public async Task<PagedResult<CounterpartResolution>> SearchActiveAsync(
        Guid userId,
        string? search,
        PartyType? type,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        string? term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var candidates = new List<CounterpartResolution>();

        if (type is null or PartyType.Employee)
        {
            candidates.AddRange(await context.Employees.AsNoTracking()
                .Where(item => item.Status == Status.Active)
                .Where(item => term == null ||
                    EF.Functions.ILike(item.FullName, $"%{term}%") ||
                    EF.Functions.ILike(item.EmployeeNumber, $"%{term}%"))
                .Select(item => new CounterpartResolution(
                    PartyType.Employee, item.Id, item.FullName, item.JobTitle, item.Status))
                .ToListAsync(cancellationToken));
        }

        if (type is null or PartyType.OrganizationalUnit)
        {
            candidates.AddRange(await context.OrganizationalUnits.AsNoTracking()
                .Where(item => item.Status == Status.Active)
                .Where(item => term == null || EF.Functions.ILike(item.Name, $"%{term}%"))
                .Select(item => new CounterpartResolution(
                    PartyType.OrganizationalUnit, item.Id, item.Name, item.UnitType, item.Status))
                .ToListAsync(cancellationToken));
        }

        if (type is null or PartyType.Site)
        {
            candidates.AddRange(await context.Sites.AsNoTracking()
                .Where(item => item.Status == Status.Active)
                .Where(item => term == null ||
                    EF.Functions.ILike(item.Name, $"%{term}%") ||
                    EF.Functions.ILike(item.Code, $"%{term}%"))
                .Select(item => new CounterpartResolution(
                    PartyType.Site, item.Id, item.Name, item.Code, item.Status))
                .ToListAsync(cancellationToken));
        }

        if (type is null or PartyType.External)
        {
            candidates.AddRange(await context.ExternalParties.AsNoTracking()
                .Where(item => item.Status == Status.Active)
                .Where(item => term == null ||
                    EF.Functions.ILike(item.NameAr, $"%{term}%") ||
                    item.Code != null && EF.Functions.ILike(item.Code, $"%{term}%"))
                .Select(item => new CounterpartResolution(
                    PartyType.External, item.Id, item.NameAr, item.Code, item.Status))
                .ToListAsync(cancellationToken));
        }

        var authorized = new List<CounterpartResolution>(candidates.Count);
        foreach (CounterpartResolution candidate in candidates)
        {
            if (await scopeAuthorizationService.CanAccessPartyAsync(
                    userId, candidate.Type, candidate.Id, cancellationToken))
            {
                authorized.Add(candidate);
            }
        }

        var ordered = authorized
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Type)
            .ThenBy(item => item.Id)
            .ToList();
        int offset = checked((page - 1) * pageSize);

        return new PagedResult<CounterpartResolution>(
            ordered.Skip(offset).Take(pageSize).ToList(), page, pageSize, ordered.Count);
    }
}
