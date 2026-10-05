using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Roles;
using Domain.Common;
using Domain.Roles;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.RolePermissions.Replace;

/// <summary>
/// Wholesale replacement of a role's permission set (RESOLUTION-027 S20/S21).
/// <para>
/// The replacement is all-or-nothing. Codes are resolved against the active catalog and rejected
/// before anything is written if any of them is unknown or could never take effect for this role,
/// because a partial or silently-inert grant is indistinguishable from a working one in the audit
/// trail and in every permission surface. The whole swap and the single aggregate version step
/// commit together.
/// </para>
/// </summary>
internal sealed class ReplaceRolePermissionsCommandHandler(
    IApplicationDbContext context,
    IApplicationTransaction transaction,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IActivePermissionCatalog activePermissionCatalog)
    : ICommandHandler<ReplaceRolePermissionsCommand, RoleResponse>
{
    public Task<Result<RoleResponse>> Handle(
        ReplaceRolePermissionsCommand command,
        CancellationToken cancellationToken) =>
        transaction.ExecuteAsync(ct => ReplaceAsync(command, ct), cancellationToken);

    private async Task<Result<RoleResponse>> ReplaceAsync(
        ReplaceRolePermissionsCommand command,
        CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Roles.Manage,
            ScopeType.Enterprise,
            scopeId: null,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<RoleResponse>(RoleErrors.Forbidden);
        }

        if (command.RoleId == WellKnownRoles.AdministratorId)
        {
            return Result.Failure<RoleResponse>(RoleErrors.BuiltInRoleImmutable);
        }

        Role? role = await context.Roles.SingleOrDefaultAsync(r => r.Id == command.RoleId, cancellationToken);

        if (role is null)
        {
            return Result.Failure<RoleResponse>(RoleErrors.NotFound(command.RoleId));
        }

        if (role.RowVersion != command.ExpectedRowVersion)
        {
            return Result.Failure<RoleResponse>(RoleErrors.RowVersionMismatch(
                role.Id, command.ExpectedRowVersion, role.RowVersion));
        }

        ScopeType[] roleScopeTypes = await context.RoleAllowedScopeTypes
            .Where(item => item.RoleId == command.RoleId)
            .Select(item => item.ScopeType)
            .ToArrayAsync(cancellationToken);

        ActivePermissionCatalog catalog = await activePermissionCatalog.GetAsync(cancellationToken);

        IReadOnlyDictionary<string, RolePermissionSetValidator.CatalogEntry> catalogByCode =
            RolePermissionSetValidator.BuildCatalog(
                catalog.Codes.Select(code => (code.PermissionId, code.Code)),
                catalog.Codes.ToDictionary(
                    code => code.PermissionId,
                    code => (IReadOnlyCollection<ScopeType>)code.AllowedScopeTypes
                        .Select(name => Enum.Parse<ScopeType>(name))
                        .ToArray()));

        IReadOnlyList<RolePermissionSetValidator.ResolvedPermission> requested =
            RolePermissionSetValidator.Validate(
                command.PermissionCodes,
                roleScopeTypes,
                catalog.Vocabulary,
                catalogByCode,
                out Error error);

        if (error != Error.None)
        {
            return Result.Failure<RoleResponse>(error);
        }

        Guid[] requestedPermissionIds = requested.Select(item => item.PermissionId).ToArray();

        List<RolePermission> existing = await context.RolePermissions
            .Where(item => item.RoleId == command.RoleId)
            .ToListAsync(cancellationToken);

        HashSet<Guid> requestedSet = requestedPermissionIds.ToHashSet();

        // Revoke first, then grant. The two sides are disjoint by construction, so the order is not
        // a correctness requirement; it keeps the diff readable when a single row is swapped.
        var revoked = existing.Where(item => !requestedSet.Contains(item.PermissionId)).ToList();
        var granted = existing.Where(item => requestedSet.Contains(item.PermissionId)).ToList();

        foreach (RolePermission row in revoked)
        {
            row.MarkAsRemoved();
            context.RolePermissions.Remove(row);
        }

        var alreadyPresent = granted.Select(item => item.PermissionId).ToHashSet();

        var toGrant =
            requested.Where(permission => !alreadyPresent.Contains(permission.PermissionId)).ToList();

        foreach (RolePermissionSetValidator.ResolvedPermission permission in toGrant)
        {
            context.RolePermissions.Add(RolePermission.Create(command.RoleId, permission.PermissionId));
        }

        // A grant already present is left untouched, and a grant outside the active vocabulary is
        // treated as absent from the requested set and therefore revoked. The vocabulary cutover
        // rewrote codes in place rather than running two parallel catalogs, so in practice the
        // revoked side is exactly "grants the client dropped".
        bool changed = revoked.Count > 0 || toGrant.Count > 0;

        // One version step for the whole replacement, so a client holding the version it read cannot
        // interleave with this change even if it edits metadata concurrently. A replacement that
        // changes nothing does NOT consume a version step, matching ExternalParty: re-submitting an
        // unchanged set after a lost response must not invalidate the caller's version.
        if (changed)
        {
            role.BumpAggregateVersion();
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            int? current = await context.Roles.AsNoTracking()
                .Where(r => r.Id == command.RoleId)
                .Select(r => (int?)r.RowVersion)
                .SingleOrDefaultAsync(cancellationToken);

            return Result.Failure<RoleResponse>(
                RoleErrors.RowVersionMismatch(command.RoleId, command.ExpectedRowVersion, current));
        }

        string[] permissionCodes = await context.RolePermissions
            .Where(item => item.RoleId == command.RoleId)
            .Join(
                context.Permissions,
                item => item.PermissionId,
                permission => permission.Id,
                (_, permission) => permission.Code)
            .OrderBy(code => code)
            .ToArrayAsync(cancellationToken);

        string[] allowedScopeTypeNames = await context.RoleAllowedScopeTypes
            .Where(item => item.RoleId == command.RoleId)
            .OrderBy(item => item.ScopeType)
            .Select(item => item.ScopeType.ToString())
            .ToArrayAsync(cancellationToken);

        return new RoleResponse
        {
            Id = role.Id,
            Name = role.Name,
            NameAr = role.NameAr,
            Description = role.Description,
            RowVersion = role.RowVersion,
            PermissionCodes = permissionCodes,
            AllowedScopeTypes = allowedScopeTypeNames
        };
    }
}