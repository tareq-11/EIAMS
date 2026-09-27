using Application.Abstractions.Authorization;
using Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Authorization;

internal sealed class AnyPermissionAuthorizationHandler(
    IEffectivePermissionService authorizationService,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<AnyPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AnyPermissionRequirement requirement)
    {
        if (context.User is not { Identity.IsAuthenticated: true })
        {
            return;
        }

        Guid userId = context.User.GetUserId();
        CancellationToken cancellationToken =
            httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;

        foreach (string permission in requirement.Permissions)
        {
            if (await authorizationService.HasPermissionAsync(userId, permission, cancellationToken))
            {
                context.Succeed(requirement);
                return;
            }
        }
    }
}
