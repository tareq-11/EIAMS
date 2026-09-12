using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Metadata;
using Web.Api.Controllers.Users;
using Web.Api.Infrastructure;

namespace IntegrationTests.Security;

public sealed class AuthRequestLimitMetadataTests
{
    public static TheoryData<Type> LimitedControllers => new()
    {
        typeof(LoginController),
        typeof(RefreshTokenController),
        typeof(LogoutController),
        typeof(RegisterController),
        typeof(RecoverAdministratorController),
        typeof(Web.Api.Controllers.Users.CreateUserController)
    };

    [Theory]
    [MemberData(nameof(LimitedControllers))]
    public void AuthenticationAndProvisioningActions_Should_HaveSmallBodyLimits(Type controllerType)
    {
        System.Reflection.MethodInfo handle = controllerType.GetMethod("Handle")
            ?? throw new ShouldAssertException($"Expected {controllerType.FullName} to expose a Handle action.");

        RequestSizeLimitAttribute? attribute = handle
            .GetCustomAttributes(typeof(RequestSizeLimitAttribute), inherit: true)
            .Cast<RequestSizeLimitAttribute>()
            .SingleOrDefault();

        attribute.ShouldNotBeNull();
        ((IRequestSizeLimitMetadata)attribute).MaxRequestBodySize
            .ShouldBe(AuthRequestLimits.MaximumBodySize);
    }
}
