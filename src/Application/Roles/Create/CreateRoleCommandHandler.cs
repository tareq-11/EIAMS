using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Roles;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Roles.Create;

/// <summary>
/// Creates a role and its complete initial grant set as one unit (RESOLUTION-027 S16/S17).
/// <para>
/// The grant set is resolved and validated before the role row is written, so a rejected code
/// cannot leave a role behind with no permissions or a partial set. Role and grants commit in a
/// single transaction, and the result is the authoritative role projection rather than a bare
/// identifier, so the client never has to immediately re-read what it just created.
/// </para>
/// </summary>
internal sealed class CreateRoleCommandHandler(
    IApplicationDbContext context,
    IApplicationTransaction transaction,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IActivePermissionCatalog activePermissionCatalog)
    : ICommandHandler<CreateRoleCommand, RoleResponse>
{
    public Task<Result<RoleResponse>> Handle(
        CreateRoleCommand command,
        CancellationToken cancellationToken) =>
        transaction.ExecuteAsync(ct => CreateAsync(command, ct), cancellationToken);

    private async Task<Result<RoleResponse>> CreateAsync(
        CreateRoleCommand command,
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

        if (await context.Roles.AnyAsync(r => r.Name == command.Name, cancellationToken))
        {
            return Result.Failure<RoleResponse>(RoleErrors.NameNotUnique);
        }

        ScopeType[] allowedScopeTypes = command.AllowedScopeTypes is { Count: > 0 }
            ? command.AllowedScopeTypes.Distinct().ToArray()
            : [ScopeType.Enterprise];

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
                allowedScopeTypes,
                catalog.Vocabulary,
                catalogByCode,
                out Error error);

        if (error != Error.None)
        {
            return Result.Failure<RoleResponse>(error);
        }

        var role = Role.Create(Guid.NewGuid(), command.Name, command.NameAr, command.Description);
        context.Roles.Add(role);

        context.RoleAllowedScopeTypes.AddRange(
            allowedScopeTypes.Select(scopeType => RoleAllowedScopeType.Create(role.Id, scopeType)));

        context.RolePermissions.AddRange(
            requested.Select(permission => RolePermission.Create(role.Id, permission.PermissionId)));

        await context.SaveChangesAsync(cancellationToken);

        return new RoleResponse
        {
            Id = role.Id,
            Name = role.Name,
            NameAr = role.NameAr,
            Description = role.Description,
            RowVersion = role.RowVersion,
            PermissionCodes = requested.Select(permission => permission.Code).ToArray(),
            AllowedScopeTypes = allowedScopeTypes
                .Select(scopeType => scopeType.ToString())
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        };
    }
}