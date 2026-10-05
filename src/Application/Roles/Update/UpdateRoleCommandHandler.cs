using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Roles;
using Domain.Common;
using Domain.Roles;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Roles.Update;

internal sealed class UpdateRoleCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<UpdateRoleCommand, RoleResponse>
{
    public async Task<Result<RoleResponse>> Handle(
        UpdateRoleCommand command,
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

        if (command.AllowedScopeTypes?.Any(scopeType => !UserAssignmentScopeTypes.IsAllowed(scopeType)) == true)
        {
            return Result.Failure<RoleResponse>(RoleErrors.OrganizationalUnitAssignmentNotAllowed);
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

        if (await context.Roles.AnyAsync(r => r.Id != command.RoleId && r.Name == command.Name, cancellationToken))
        {
            return Result.Failure<RoleResponse>(RoleErrors.NameNotUnique);
        }

        role.UpdateDetails(command.Name, command.NameAr, command.Description);

        if (command.AllowedScopeTypes is not null)
        {
            ScopeType[] allowedScopeTypes = command.AllowedScopeTypes.Distinct().ToArray();

            // A seeded role's scope types are fixed at creation. Owner ruling 2026-10-05: widening
            // one lets any caller holding roles.manage promote a built-in role to a scope it was
            // never meant to occupy, which is escalation rather than editing. Checked against the
            // stored set rather than merely rejecting the field, so a client that always submits the
            // current scopes - as the role form does - keeps working on seeded roles.
            if (WellKnownRoles.IsSeeded(role.Id))
            {
                ScopeType[] storedScopeTypes = await context.RoleAllowedScopeTypes
                    .Where(item => item.RoleId == role.Id)
                    .Select(item => item.ScopeType)
                    .ToArrayAsync(cancellationToken);

                if (!storedScopeTypes.OrderBy(scopeType => scopeType).SequenceEqual(
                        allowedScopeTypes.OrderBy(scopeType => scopeType)))
                {
                    return Result.Failure<RoleResponse>(
                        RoleErrors.SeededRoleScopeTypesImmutable(
                            role.Id,
                            allowedScopeTypes
                                .Select(scopeType => scopeType.ToString())
                                .OrderBy(name => name, StringComparer.Ordinal)
                                .ToArray()));
                }
            }

            bool invalidatesExistingAssignments = await context.UserRoleScopes
                .AsNoTracking()
                .AnyAsync(
                    assignment => assignment.RoleId == command.RoleId &&
                                  !allowedScopeTypes.Contains(assignment.ScopeType),
                    cancellationToken);

            if (invalidatesExistingAssignments)
            {
                return Result.Failure<RoleResponse>(
                    RoleErrors.AllowedScopeTypesConflictWithAssignments(command.RoleId));
            }

            List<RoleAllowedScopeType> existingAllowedScopeTypes = await context.RoleAllowedScopeTypes
                .Where(item => item.RoleId == command.RoleId)
                .ToListAsync(cancellationToken);

            context.RoleAllowedScopeTypes.RemoveRange(existingAllowedScopeTypes);
            context.RoleAllowedScopeTypes.AddRange(
                allowedScopeTypes.Select(scopeType => RoleAllowedScopeType.Create(command.RoleId, scopeType)));
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another writer committed between the read above and this save. Report the version
            // that actually won so the client can reload rather than replay a stale form.
            int? current = await context.Roles.AsNoTracking()
                .Where(r => r.Id == command.RoleId)
                .Select(r => (int?)r.RowVersion)
                .SingleOrDefaultAsync(cancellationToken);

            return Result.Failure<RoleResponse>(
                RoleErrors.RowVersionMismatch(command.RoleId, command.ExpectedRowVersion, current));
        }

        string[] allowedScopeTypeNames = await context.RoleAllowedScopeTypes
            .Where(item => item.RoleId == command.RoleId)
            .OrderBy(item => item.ScopeType)
            .Select(item => item.ScopeType.ToString())
            .ToArrayAsync(cancellationToken);

        // A metadata update does not change membership, so the codes are read back rather than
        // tracked: this keeps the returned projection identical to what a subsequent GET would
        // produce, which is what lets the client adopt it instead of re-fetching.
        string[] permissionCodes = await context.RolePermissions
            .Where(item => item.RoleId == command.RoleId)
            .Join(
                context.Permissions,
                item => item.PermissionId,
                permission => permission.Id,
                (_, permission) => permission.Code)
            .OrderBy(code => code)
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