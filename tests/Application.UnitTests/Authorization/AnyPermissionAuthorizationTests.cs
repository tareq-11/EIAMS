using System.Security.Claims;
using Application.Abstractions.Authorization;
using Infrastructure.Authentication;
using Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Application.UnitTests.Authorization;

public sealed class AnyPermissionAuthorizationTests
{
    private static readonly string[] Permissions =
    [
        PermissionCodes.InventoryCounts.Plan,
        PermissionCodes.InventoryCounts.Complete,
        PermissionCodes.InventoryCounts.Close
    ];

    [Theory]
    [InlineData("count.plan")]
    [InlineData("count.complete")]
    [InlineData("count.close")]
    public async Task Handler_ShouldSucceedWhenAnyOnePermissionIsPresent(string grantedPermission)
    {
        IEffectivePermissionService service = Substitute.For<IEffectivePermissionService>();
        service.HasPermissionAsync(Arg.Any<Guid>(), grantedPermission, Arg.Any<CancellationToken>()).Returns(true);
        AnyPermissionAuthorizationHandler handler = CreateHandler(service);
        AnyPermissionRequirement requirement = new(Permissions);
        AuthorizationHandlerContext context = CreateContext(requirement);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Handler_ShouldDenyWhenNoneIsPresentOrUserIsAnonymous()
    {
        IEffectivePermissionService service = Substitute.For<IEffectivePermissionService>();
        AnyPermissionAuthorizationHandler handler = CreateHandler(service);
        AnyPermissionRequirement requirement = new(Permissions);
        AuthorizationHandlerContext context = CreateContext(requirement, authenticated: true);

        await handler.HandleAsync(context);
        context.HasSucceeded.ShouldBeFalse();

        AuthorizationHandlerContext anonymous = CreateContext(requirement, authenticated: false);
        await handler.HandleAsync(anonymous);
        anonymous.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Provider_ShouldParseAnyPolicyAndRejectMalformedEmptyPolicy()
    {
        PermissionAuthorizationPolicyProvider provider = new(Options.Create(new AuthorizationOptions()));

        AuthorizationPolicy? policy = await provider.GetPolicyAsync("any:count.plan|count.complete|count.close");
        policy.ShouldNotBeNull();
        policy.Requirements.Single().ShouldBeOfType<AnyPermissionRequirement>();

        AuthorizationPolicy? malformed = await provider.GetPolicyAsync("any:");
        malformed.ShouldNotBeNull();
        AnyPermissionRequirement requirement = malformed.Requirements.Single().ShouldBeOfType<AnyPermissionRequirement>();
        requirement.Permissions.ShouldBeEmpty();
    }

    private static AnyPermissionAuthorizationHandler CreateHandler(IEffectivePermissionService service)
    {
        IHttpContextAccessor accessor = Substitute.For<IHttpContextAccessor>();
        return new AnyPermissionAuthorizationHandler(service, accessor);
    }

    private static AuthorizationHandlerContext CreateContext(
        AnyPermissionRequirement requirement,
        bool authenticated = true)
    {
        var userId = Guid.NewGuid();
        ClaimsIdentity identity = new(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            authenticated ? "Test" : null);
        ClaimsPrincipal principal = new(identity);
        return new AuthorizationHandlerContext([requirement], principal, null);
    }
}
