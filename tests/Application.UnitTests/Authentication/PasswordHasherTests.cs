using System.Security.Cryptography;
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

        hash.ShouldStartWith("pbkdf2-sha512$500000$");
        hasher.Verify("correct-password", hash).ShouldBeTrue();
        hasher.Verify("wrong-password", hash).ShouldBeFalse();
        hasher.NeedsRehash(hash).ShouldBeFalse();
    }

    [Fact]
    public void Verify_Should_AcceptLegacyHashAndMarkItForUpgrade()
    {
        const string password = "legacy-password";
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            500000,
            HashAlgorithmName.SHA512,
            32);
        string legacyHash = $"{Convert.ToHexString(hash)}-{Convert.ToHexString(salt)}";

        hasher.Verify(password, legacyHash).ShouldBeTrue();
        hasher.Verify("wrong-password", legacyHash).ShouldBeFalse();
        hasher.NeedsRehash(legacyHash).ShouldBeTrue();
    }

    [Fact]
    public void Verify_Should_RejectVersionedHashWithExcessiveIterationCount()
    {
        string maliciousHash = $"pbkdf2-sha512$1000001${new string('0', 64)}${new string('0', 32)}";

        hasher.Verify("password", maliciousHash).ShouldBeFalse();
    }
}
