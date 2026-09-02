using Domain.Common;

namespace Application.Abstractions.Authorization;

/// <summary>
/// Resolves the user's single role/scope assignment, its permissions, and hierarchical containment.
/// </summary>
public interface IScopeAuthorizationService
{
    Task<UserAuthorizationAssignment?> GetUserAssignmentAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> HasPermissionAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken);

    Task<bool> HasPermissionInScopeAsync(
        Guid userId,
        string permission,
        ScopeType scopeType,
        Guid? scopeId,
        CancellationToken cancellationToken);

    Task<WarehousePermissionScope> GetWarehousePermissionScopeAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken);

    Task<SitePermissionScope> GetSitePermissionScopeAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken);

    Task<OrganizationalUnitPermissionScope> GetOrganizationalUnitPermissionScopeAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken);

    Task<bool> CanAccessSiteAsync(Guid userId, Guid siteId, CancellationToken cancellationToken);

    Task<bool> CanAccessOrganizationalUnitAsync(
        Guid userId,
        Guid organizationalUnitId,
        CancellationToken cancellationToken);

    Task<bool> CanAccessWarehouseAsync(
        Guid userId,
        Guid warehouseId,
        CancellationToken cancellationToken);

    Task<bool> CanAccessPartyAsync(
        Guid userId,
        PartyType partyType,
        Guid partyId,
        CancellationToken cancellationToken);

    Task<PartyAccessScope> GetPartyAccessScopeAsync(
        Guid userId,
        CancellationToken cancellationToken);
}

public sealed record UserAuthorizationAssignment(
    Guid Id,
    Guid UserId,
    Guid RoleId,
    ScopeType ScopeType,
    Guid? ScopeId);

public sealed record SitePermissionScope(bool HasEnterpriseAccess, IReadOnlySet<Guid> SiteIds);

public sealed record OrganizationalUnitPermissionScope(
    bool HasEnterpriseAccess,
    IReadOnlySet<Guid> OrganizationalUnitIds);

public sealed record WarehousePermissionScope(
    bool HasEnterpriseAccess,
    IReadOnlySet<Guid> WarehouseIds);

public sealed record PartyAccessScope(
    bool HasAssignment,
    bool HasEnterpriseAccess,
    IReadOnlySet<Guid> SiteIds,
    IReadOnlySet<Guid> OrganizationalUnitIds);
