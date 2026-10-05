using Application.Abstractions.Authorization;
using Domain.Common;
using Domain.Roles;
using SharedKernel;

namespace Application.Roles;

/// <summary>
/// Resolves submitted dotted permission codes against the authoritative catalog and rejects codes
/// that could never take effect for the target role.
/// <para>
/// Shared by role creation and wholesale permission replacement so the two cannot drift: a code
/// accepted at creation must stay acceptable at replacement, and the reverse.
/// </para>
/// </summary>
internal static class RolePermissionSetValidator
{
    /// <summary>A catalogued permission code and the scope types it is valid at.</summary>
    public sealed record CatalogEntry(Guid PermissionId, string Code, IReadOnlyCollection<ScopeType> AllowedScopeTypes);

    /// <summary>A submitted code that passed validation, resolved to its catalog row.</summary>
    public sealed record ResolvedPermission(Guid PermissionId, string Code);

    /// <summary>
    /// Validates submitted codes against the active vocabulary and the role's allowed scope types.
    /// </summary>
    /// <param name="submittedCodes">Codes as submitted; blanks and duplicates are tolerated.</param>
    /// <param name="roleScopeTypes">Scope types the role may be assigned at.</param>
    /// <param name="activeVocabulary">Vocabulary selected by the active policy marker.</param>
    /// <param name="catalog">Catalogued permissions keyed by code.</param>
    /// <param name="error">The rejection, or <see cref="Error.None"/> when the set is acceptable.</param>
    /// <returns>Distinct resolved permissions in code order, or an empty list on rejection.</returns>
    public static IReadOnlyList<ResolvedPermission> Validate(
        IReadOnlyCollection<string> submittedCodes,
        IReadOnlyCollection<ScopeType> roleScopeTypes,
        string activeVocabulary,
        IReadOnlyDictionary<string, CatalogEntry> catalog,
        out Error error)
    {
        error = Error.None;

        // Only the marker-selected vocabulary is effective, so only those codes can ever grant
        // anything. Reading the marker instead of hard-coding the set keeps this correct across a
        // policy cutover, matching how effective grants are computed.
        IReadOnlyCollection<string> effectiveCodes = activeVocabulary switch
        {
            "dotted-v1" => PermissionVocabulary.DottedV1Codes,
            "legacy-colon" => PermissionVocabulary.LegacyColonCodes,
            _ => []
        };

        string[] distinctCodes = submittedCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        string[] unknownCodes = distinctCodes
            .Where(code => !effectiveCodes.Contains(code, StringComparer.Ordinal))
            .ToArray();

        if (unknownCodes.Length > 0)
        {
            error = RoleErrors.UnknownPermissionCodes(unknownCodes);
            return [];
        }

        var incompatible = new List<RoleErrors.IncompatiblePermissionCode>();

        foreach (string code in distinctCodes)
        {
            if (!catalog.TryGetValue(code, out CatalogEntry? entry))
            {
                error = RoleErrors.UnknownPermissionCodes([code]);
                return [];
            }

            bool overlaps = entry.AllowedScopeTypes.Any(roleScopeTypes.Contains);

            if (!overlaps)
            {
                incompatible.Add(new RoleErrors.IncompatiblePermissionCode(
                    code,
                    entry.AllowedScopeTypes
                        .Select(scopeType => scopeType.ToString())
                        .OrderBy(name => name, StringComparer.Ordinal)
                        .ToArray()));
            }
        }

        if (incompatible.Count > 0)
        {
            error = RoleErrors.PermissionCodesNotAllowedForRoleScopes(incompatible);
            return [];
        }

        return distinctCodes
            .Select(code => new ResolvedPermission(catalog[code].PermissionId, code))
            .ToArray();
    }

    /// <summary>
    /// Builds the catalog lookup from the permission rows and their allowed scope types.
    /// </summary>
    public static IReadOnlyDictionary<string, CatalogEntry> BuildCatalog(
        IEnumerable<(Guid Id, string Code)> permissions,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<ScopeType>> scopesByPermissionId)
    {
        var catalog = new Dictionary<string, CatalogEntry>(StringComparer.Ordinal);

        foreach ((Guid id, string code) in permissions)
        {
            catalog[code] = new CatalogEntry(
                id,
                code,
                scopesByPermissionId.TryGetValue(id, out IReadOnlyCollection<ScopeType>? scopes) ? scopes : []);
        }

        return catalog;
    }
}