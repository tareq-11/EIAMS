using Application.Abstractions.Authorization;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Authorization;

/// <summary>
/// Database-backed <see cref="IActivePermissionCatalog"/>: reads the active policy vocabulary and
/// the catalog in one round-trip pair.
/// <para>
/// The active marker is migration-controlled state, so it is read here in Infrastructure rather than
/// exposed through the application data context. The vocabulary is validated against the compiled-in
/// code lists by <c>RolePermissionSetValidator</c>, so a marker naming a vocabulary this build does
/// not know yields an empty effective set instead of silently granting codes the running policy
/// cannot honour.
/// </para>
/// <para>
/// Named <c>DbActivePermissionCatalog</c> rather than <c>ActivePermissionCatalog</c> because the
/// latter is the contract record this type returns; sharing the name made every unqualified
/// reference ambiguous.
/// </para>
/// </summary>
internal sealed class DbActivePermissionCatalog(ApplicationDbContext context) : IActivePermissionCatalog
{
    public async Task<ActivePermissionCatalog> GetAsync(CancellationToken cancellationToken)
    {
        string vocabulary = await context.AuthorizationPolicyVersions
            .AsNoTracking()
            .Where(marker => marker.IsActive)
            .Select(marker => marker.ActiveVocabulary)
            .SingleAsync(cancellationToken);

        // LEFT JOIN, not an inner join: a catalogued code that currently has no
        // permission_allowed_scope_types row is still a real, active-vocabulary code, it simply
        // cannot take effect anywhere. Dropping it here would make it indistinguishable from a code
        // that does not exist, so the caller would report "unknown permission code" instead of the
        // accurate "this permission cannot be granted to a role at these scopes".
        var rows = await (
                from permission in context.Permissions.AsNoTracking()
                join allowedScope in context.PermissionAllowedScopeTypes.AsNoTracking()
                    on permission.Id equals allowedScope.PermissionId
                    into allowedScopes
                from allowedScope in allowedScopes.DefaultIfEmpty()
                group allowedScope by new { permission.Id, permission.Code } into grouped
                select new
                {
                    grouped.Key.Id,
                    grouped.Key.Code,
                    ScopeTypes = grouped
                        .Select(item => item == null ? null : item.ScopeType.ToString())
                        .Where(name => name != null)
                        .OrderBy(name => name)
                        .ToList()
                })
            .ToListAsync(cancellationToken);

        var codes = new List<ActivePermissionCode>(rows.Count);
        foreach (var row in rows)
        {
            codes.Add(new ActivePermissionCode(row.Id, row.Code, row.ScopeTypes));
        }

        return new ActivePermissionCatalog(vocabulary, codes);
    }
}