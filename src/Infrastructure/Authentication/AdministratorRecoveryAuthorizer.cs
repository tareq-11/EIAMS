using System.Security.Cryptography;
using Application.Abstractions.Authentication;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Infrastructure.Authentication;

internal sealed class AdministratorRecoveryAuthorizer(
    IOptions<AdministratorRecoveryOptions> options,
    IDateTimeProvider dateTimeProvider) : IAdministratorRecoveryAuthorizer
{
    private readonly AdministratorRecoveryOptions options = options.Value;
    private readonly byte[] expectedToken = AdministratorRecoveryOptions.TryDecodeToken(
        options.Value.Token,
        out byte[] configuredToken)
        ? configuredToken
        : [];

    public bool IsAuthorized(string? recoveryToken)
    {
        if (!options.Enabled ||
            options.ExpiresAtUtc.Offset != TimeSpan.Zero ||
            options.ExpiresAtUtc.UtcDateTime <= dateTimeProvider.UtcNow ||
            !AdministratorRecoveryOptions.TryDecodeToken(recoveryToken, out byte[] suppliedToken))
        {
            return false;
        }

        return expectedToken.Length == AdministratorRecoveryOptions.TokenBytes &&
               CryptographicOperations.FixedTimeEquals(expectedToken, suppliedToken);
    }
}
