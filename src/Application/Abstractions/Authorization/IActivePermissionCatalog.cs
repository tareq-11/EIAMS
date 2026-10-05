namespace Application.Abstractions.Authorization;

/// <summary>A permission code the active policy vocabulary recognises, with the scope types it is valid at.</summary>
/// <param name="PermissionId">Catalog row id, used to build role grants.</param>
/// <param name="Code">The dotted (or legacy) permission code.</param>
/// <param name="AllowedScopeTypes">Scope type names the permission is valid at.</param>
public sealed record ActivePermissionCode(Guid PermissionId, string Code, IReadOnlyCollection<string> AllowedScopeTypes);

/// <summary>
/// The permission catalog as the active policy marker selects it.
/// </summary>
/// <param name="Vocabulary">The marker-selected vocabulary name, for example <c>dotted-v1</c>.</param>
/// <param name="Codes">Every catalogued code that vocabulary makes effective.</param>
public sealed record ActivePermissionCatalog(string Vocabulary, IReadOnlyList<ActivePermissionCode> Codes);

/// <summary>
/// Reads the permission catalog as the active authorization policy marker selects it.
/// <para>
/// Role administration must grant only codes that can actually take effect, and the effective set
/// is decided by the marker rather than by the code list. Reading the marker here keeps the
/// Application layer free of the migration-controlled marker entity and keeps grant validation
/// consistent with how <c>ScopeAuthorizationService</c> computes effective permissions.
/// </para>
/// </summary>
public interface IActivePermissionCatalog
{
    Task<ActivePermissionCatalog> GetAsync(CancellationToken cancellationToken);
}