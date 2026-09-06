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
        typeof(CreateUserController)
    };

    [Theory]
    [MemberData(nameof(LimitedControllers))]
    public void AuthenticationAndProvisioningActions_Should_HaveSmallBodyLimits(Type controllerType)
    {
        RequestSizeLimitAttribute? attribute = controllerType
            .GetMethod("Handle")!
            .GetCustomAttributes(typeof(RequestSizeLimitAttribute), inherit: true)
            .Cast<RequestSizeLimitAttribute>()
            .SingleOrDefault();

        attribute.ShouldNotBeNull();
        ((IRequestSizeLimitMetadata)attribute).MaxRequestBodySize
            .ShouldBe(AuthRequestLimits.MaximumBodySize);
    }
}
