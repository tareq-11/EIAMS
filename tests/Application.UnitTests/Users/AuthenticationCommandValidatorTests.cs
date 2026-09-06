using Application.Users.Login;
using Application.Users.Logout;
using Application.Users.Refresh;

namespace Application.UnitTests.Users;

public sealed class AuthenticationCommandValidatorTests
{
    [Fact]
    public async Task Login_Should_RejectOversizedOrNonAsciiCredentials()
    {
        var validator = new LoginUserCommandValidator();

        (await validator.ValidateAsync(new LoginUserCommand("user@example.com", new string('x', 129))))
            .IsValid.ShouldBeFalse();
        (await validator.ValidateAsync(new LoginUserCommand("مستخدم@example.com", "password")))
            .IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Refresh_Should_RejectOversizedToken()
    {
        var validator = new RefreshTokenCommandValidator();

        (await validator.ValidateAsync(new RefreshTokenCommand(new string('x', 257))))
            .IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Logout_Should_AcceptEmptyTokenAndRejectOversizedToken()
    {
        var validator = new LogoutUserCommandValidator();

        (await validator.ValidateAsync(new LogoutUserCommand(null))).IsValid.ShouldBeTrue();
        (await validator.ValidateAsync(new LogoutUserCommand(new string('x', 257))))
            .IsValid.ShouldBeFalse();
    }
}
