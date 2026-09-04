using Infrastructure.Authentication;

namespace Application.UnitTests.Authentication;

public sealed class PasswordHasherTests
{
    private readonly PasswordHasher hasher = new();

    [Fact]
    public void Verify_Should_ReturnFalse_WhenStoredHashHasInvalidShape()
    {
        hasher.Verify("password", "not-a-valid-hash").ShouldBeFalse();
    }

    [Fact]
    public void Verify_Should_ReturnFalse_WhenStoredHashContainsNonHexCharacters()
    {
        string malformedHash = $"{new string('Z', 64)}-{new string('Z', 32)}";

        hasher.Verify("password", malformedHash).ShouldBeFalse();
    }

    [Fact]
    public void HashAndVerify_Should_AcceptOnlyTheOriginalPassword()
    {
        string hash = hasher.Hash("correct-password");

        hasher.Verify("correct-password", hash).ShouldBeTrue();
        hasher.Verify("wrong-password", hash).ShouldBeFalse();
    }
}
