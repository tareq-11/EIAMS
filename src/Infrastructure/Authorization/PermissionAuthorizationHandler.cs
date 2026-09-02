using Application.Abstractions.Authorization;
using Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Authorization;

internal sealed class PermissionAuthorizationHandler(
    IServiceScopeFactory serviceScopeFactory,
    IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User is not { Identity.IsAuthenticated: true })
        {
            return;
        }

        using IServiceScope scope = serviceScopeFactory.CreateScope();

        IScopeAuthorizationService authorizationService =
            scope.ServiceProvider.GetRequiredService<IScopeAuthorizationService>();

        Guid userId = context.User.GetUserId();
        CancellationToken cancellationToken =
            httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;

        if (await authorizationService.HasPermissionAsync(
                userId,
                requirement.Permission,
                cancellationToken))
        {
            context.Succeed(requirement);
        }
    }
}
