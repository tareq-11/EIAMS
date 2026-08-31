using Application.Abstractions.Authorization;
using Domain.Common;
using Domain.Users;
using SharedKernel;

namespace Application.Users;

internal static class UserAdministrationAuthorization
{
    public static async Task<Result> EnsureEnterpriseAccessAsync(
        IScopeAuthorizationService scopeAuthorizationService,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        bool hasPermission = await scopeAuthorizationService.HasPermissionAsync(
            actorUserId,
            PermissionCodes.Users.Access,
            cancellationToken);

        UserAuthorizationAssignment? assignment = await scopeAuthorizationService.GetUserAssignmentAsync(
            actorUserId,
            cancellationToken);

        return hasPermission && assignment?.ScopeType == ScopeType.Enterprise
            ? Result.Success()
            : Result.Failure(UserErrors.AdministrationRequiresEnterpriseScope);
    }
}
