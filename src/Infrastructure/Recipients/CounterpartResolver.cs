using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Pagination;
using Application.Abstractions.Recipients;
using Application.Abstractions.Searching;
using Domain.Common;
using Domain.Custodies;
using Domain.Employees;
using Domain.ExternalParties;
using Domain.OrganizationalUnits;
using Domain.Sites;
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

    public async Task<IReadOnlyDictionary<CounterpartReference, CounterpartResolution>> ResolveManyAsync(
        IReadOnlyCollection<CounterpartReference> counterparts,
        CancellationToken cancellationToken)
    {
        CounterpartReference[] references = counterparts
            .Where(reference => Enum.IsDefined(reference.Type) && reference.Id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (references.Length == 0)
        {
            return new Dictionary<CounterpartReference, CounterpartResolution>();
        }

        var resolutions = new Dictionary<CounterpartReference, CounterpartResolution>(references.Length);

        Guid[] employeeIds = GetIds(PartyType.Employee);
        if (employeeIds.Length > 0)
        {
            List<CounterpartResolution> employees = await context.Employees.AsNoTracking()
                .Where(item => employeeIds.Contains(item.Id))
                .Select(item => new CounterpartResolution(
                    PartyType.Employee, item.Id, item.FullName, item.JobTitle, item.Status))
                .ToListAsync(cancellationToken);
            AddToDictionary(employees);
        }

        Guid[] organizationalUnitIds = GetIds(PartyType.OrganizationalUnit);
        if (organizationalUnitIds.Length > 0)
        {
            List<CounterpartResolution> organizationalUnits = await context.OrganizationalUnits.AsNoTracking()
                .Where(item => organizationalUnitIds.Contains(item.Id))
                .Select(item => new CounterpartResolution(
                    PartyType.OrganizationalUnit, item.Id, item.Name, item.UnitType, item.Status))
                .ToListAsync(cancellationToken);
            AddToDictionary(organizationalUnits);
        }

        Guid[] siteIds = GetIds(PartyType.Site);
        if (siteIds.Length > 0)
        {
            List<CounterpartResolution> sites = await context.Sites.AsNoTracking()
                .Where(item => siteIds.Contains(item.Id))
                .Select(item => new CounterpartResolution(
                    PartyType.Site, item.Id, item.Name, item.Code, item.Status))
                .ToListAsync(cancellationToken);
            AddToDictionary(sites);
        }

        Guid[] externalPartyIds = GetIds(PartyType.External);
        if (externalPartyIds.Length > 0)
        {
            List<CounterpartResolution> externalParties = await context.ExternalParties.AsNoTracking()
                .Where(item => externalPartyIds.Contains(item.Id))
                .Select(item => new CounterpartResolution(
                    PartyType.External, item.Id, item.NameAr, item.Code, item.Status))
                .ToListAsync(cancellationToken);
            AddToDictionary(externalParties);
        }

        return resolutions;

        Guid[] GetIds(PartyType partyType) => references
            .Where(reference => reference.Type == partyType)
            .Select(reference => reference.Id)
            .ToArray();

        void AddToDictionary(IEnumerable<CounterpartResolution> items)
        {
            foreach (CounterpartResolution item in items)
            {
                resolutions[new CounterpartReference(item.Type, item.Id)] = item;
            }
        }
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
        string? term = SqlLikePattern.CreateContains(search);
        int normalizedPage = page <= 0 ? 1 : page;
        int normalizedPageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);
        int offset = checked((normalizedPage - 1) * normalizedPageSize);
        PartyAccessScope access = await scopeAuthorizationService.GetPartyAccessScopeAsync(
            userId,
            cancellationToken);

        if (!access.HasAssignment)
        {
            return new PagedResult<CounterpartResolution>(
                [], normalizedPage, normalizedPageSize, 0);
        }

        Guid[] allowedSiteIds = access.SiteIds.ToArray();
        Guid[] allowedOrganizationalUnitIds = access.OrganizationalUnitIds.ToArray();
        IQueryable<CounterpartSearchRow>? candidates = null;

        if (type is null or PartyType.Employee)
        {
            IQueryable<Employee> employeeQuery = context.Employees.AsNoTracking()
                .Where(item => item.Status == Status.Active)
                .Where(item => term == null ||
                    EF.Functions.ILike(item.FullName, term, SqlLikePattern.EscapeCharacter) ||
                    EF.Functions.ILike(item.EmployeeNumber, term, SqlLikePattern.EscapeCharacter));

            if (!access.HasEnterpriseAccess)
            {
                employeeQuery = employeeQuery.Where(
                    item => allowedOrganizationalUnitIds.Contains(item.OrgUnitId));
            }

            IQueryable<CounterpartSearchRow> employees = employeeQuery
                .Select(item => new CounterpartSearchRow
                {
                    Type = PartyType.Employee,
                    Id = item.Id,
                    DisplayName = item.FullName,
                    SecondaryLabelAr = item.JobTitle,
                    Status = item.Status
                });
            candidates = Append(candidates, employees);
        }

        if (type is null or PartyType.OrganizationalUnit)
        {
            IQueryable<OrganizationalUnit> unitQuery = context.OrganizationalUnits.AsNoTracking()
                .Where(item => item.Status == Status.Active)
                .Where(item => term == null || EF.Functions.ILike(item.Name, term, SqlLikePattern.EscapeCharacter));

            if (!access.HasEnterpriseAccess)
            {
                unitQuery = unitQuery.Where(item => allowedOrganizationalUnitIds.Contains(item.Id));
            }

            IQueryable<CounterpartSearchRow> organizationalUnits = unitQuery
                .Select(item => new CounterpartSearchRow
                {
                    Type = PartyType.OrganizationalUnit,
                    Id = item.Id,
                    DisplayName = item.Name,
                    SecondaryLabelAr = item.UnitType,
                    Status = item.Status
                });
            candidates = Append(candidates, organizationalUnits);
        }

        if (type is null or PartyType.Site)
        {
            IQueryable<Site> siteQuery = context.Sites.AsNoTracking()
                .Where(item => item.Status == Status.Active)
                .Where(item => term == null ||
                    EF.Functions.ILike(item.Name, term, SqlLikePattern.EscapeCharacter) ||
                    EF.Functions.ILike(item.Code, term, SqlLikePattern.EscapeCharacter));

            if (!access.HasEnterpriseAccess)
            {
                siteQuery = siteQuery.Where(item => allowedSiteIds.Contains(item.Id));
            }

            IQueryable<CounterpartSearchRow> sites = siteQuery
                .Select(item => new CounterpartSearchRow
                {
                    Type = PartyType.Site,
                    Id = item.Id,
                    DisplayName = item.Name,
                    SecondaryLabelAr = item.Code,
                    Status = item.Status
                });
            candidates = Append(candidates, sites);
        }

        if (type is null or PartyType.External)
        {
            IQueryable<ExternalParty> externalQuery = context.ExternalParties.AsNoTracking()
                .Where(item => item.Status == Status.Active)
                .Where(item => term == null ||
                    EF.Functions.ILike(item.NameAr, term, SqlLikePattern.EscapeCharacter) ||
                    item.Code != null && EF.Functions.ILike(item.Code, term, SqlLikePattern.EscapeCharacter));

            IQueryable<CounterpartSearchRow> externalParties = externalQuery
                .Select(item => new CounterpartSearchRow
                {
                    Type = PartyType.External,
                    Id = item.Id,
                    DisplayName = item.NameAr,
                    SecondaryLabelAr = item.Code,
                    Status = item.Status
                });
            candidates = Append(candidates, externalParties);
        }

        if (candidates is null)
        {
            return new PagedResult<CounterpartResolution>(
                [], normalizedPage, normalizedPageSize, 0);
        }

        int totalCount = await candidates.CountAsync(cancellationToken);
        List<CounterpartResolution> items = await candidates
            .OrderBy(item => item.DisplayName)
            .ThenBy(item => item.Type)
            .ThenBy(item => item.Id)
            .Skip(offset)
            .Take(normalizedPageSize)
            .Select(item => new CounterpartResolution(
                item.Type,
                item.Id,
                item.DisplayName,
                item.SecondaryLabelAr,
                item.Status))
            .ToListAsync(cancellationToken);

        return new PagedResult<CounterpartResolution>(
            items,
            normalizedPage,
            normalizedPageSize,
            totalCount);
    }

    private static IQueryable<CounterpartSearchRow> Append(
        IQueryable<CounterpartSearchRow>? current,
        IQueryable<CounterpartSearchRow> next) =>
        current is null ? next : current.Concat(next);

    private sealed class CounterpartSearchRow
    {
        public PartyType Type { get; init; }

        public Guid Id { get; init; }

        public string DisplayName { get; init; } = string.Empty;

        public string? SecondaryLabelAr { get; init; }

        public Status Status { get; init; }
    }
}
