using Application.Users.Login;
using Application.Users.Logout;
using Application.Users.Refresh;
using Application.Users.Create;
using Domain.Users;

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

    [Fact]
    public async Task Username_Should_NormalizeNfkcTrimAndCase_AndRejectInvalidInput()
    {
        User.NormalizeUsername("  ＡdMin_01  ").ShouldBe("admin_01");

        var validator = new CreateUserCommandValidator();
        (await validator.ValidateAsync(new CreateUserCommand("user@example.com", "مستخدم", "Test", "User", "Password1!")))
            .IsValid.ShouldBeFalse();
        (await validator.ValidateAsync(new CreateUserCommand("user@example.com", "invalid space", "Test", "User", "Password1!")))
            .IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task LoginUsername_Should_AcceptNfkcCompatibleFullwidthInput_ButRejectArabic()
    {
        var validator = new LoginUserCommandValidator();

        (await validator.ValidateAsync(new LoginUserCommand("  ＡdMin_01  ", "Password1!")))
            .IsValid.ShouldBeTrue();
        (await validator.ValidateAsync(new LoginUserCommand("مستخدم", "Password1!")))
            .IsValid.ShouldBeFalse();
    }
}
